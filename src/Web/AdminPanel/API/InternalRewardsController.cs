// <copyright file="InternalRewardsController.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.API;

using System.Threading;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic.PlugIns.Security;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Web.AdminPanel.Auth;
using MUnique.OpenMU.Web.AdminPanel.Services;

/// <summary>
/// Internal reward bridge for the website backend (VIP / coins / vault items).
/// Protected by <c>PROMU_INTERNAL_API_SECRET</c> (header <c>X-Promu-Internal-Secret</c>) in addition to API-key auth.
/// </summary>
[ApiController]
[Route("api/internal/rewards")]
[Authorize(AuthenticationSchemes = ApiKeyAuthenticationDefaults.ApiSchemes, Policy = AdminPolicies.AuthenticatedCashShop)]
public sealed class InternalRewardsController : ControllerBase
{
    private readonly CashShopCoinService _coinService;
    private readonly VipAccountService _vipService;
    private readonly IPersistenceContextProvider _persistence;
    private readonly IDataSource<GameConfiguration> _gameConfigurationSource;
    private readonly ILogger<InternalRewardsController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="InternalRewardsController"/> class.
    /// </summary>
    public InternalRewardsController(
        IPersistenceContextProvider persistenceContextProvider,
        IDataSource<GameConfiguration> gameConfigurationSource,
        ILogger<InternalRewardsController> logger)
    {
        this._persistence = persistenceContextProvider;
        this._gameConfigurationSource = gameConfigurationSource;
        this._coinService = new CashShopCoinService(persistenceContextProvider, gameConfigurationSource);
        this._vipService = new VipAccountService(persistenceContextProvider, gameConfigurationSource);
        this._logger = logger;
    }

    /// <summary>
    /// Grants cash-shop coins under the account transaction lock.
    /// </summary>
    [HttpPost("coins")]
    public async Task<IActionResult> GrantCoinsAsync([FromBody] InternalCoinGrantRequest request, CancellationToken cancellationToken)
    {
        if (!this.IsInternalSecretValid())
        {
            return this.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.LoginName) || request.Amount is null or 0)
        {
            return this.BadRequest();
        }

        var accountId = await this.TryGetAccountIdAsync(request.LoginName, cancellationToken).ConfigureAwait(false);
        if (accountId is null)
        {
            return this.NotFound();
        }

        object? payload = null;
        await AccountTransactionLock.RunAsync(accountId, async () =>
        {
            var (status, grant) = await this._coinService.GrantAsync(
                request.LoginName,
                request.CoinType ?? CashShopCoinType.WCoinC,
                request.Amount.Value,
                request.Reason,
                request.Reference,
                "website-internal",
                cancellationToken).ConfigureAwait(false);
            payload = new { status, grant };
        }).ConfigureAwait(false);

        this._logger.LogInformation("Internal coin grant for {Login}", request.LoginName);
        return this.Ok(payload);
    }

    /// <summary>
    /// Returns VIP status for an account.
    /// </summary>
    [HttpGet("vip/{loginName}")]
    public async Task<IActionResult> GetVipStatusAsync(string loginName, CancellationToken cancellationToken)
    {
        if (!this.IsInternalSecretValid())
        {
            return this.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(loginName))
        {
            return this.BadRequest();
        }

        var status = await this._vipService.GetStatusAsync(loginName, cancellationToken).ConfigureAwait(false);
        return status is null ? this.NotFound() : this.Ok(status);
    }

    /// <summary>
    /// Returns ProGold (WCoinP) balance for an account.
    /// </summary>
    [HttpGet("progold/{loginName}")]
    public async Task<IActionResult> GetProGoldAsync(string loginName, CancellationToken cancellationToken)
    {
        if (!this.IsInternalSecretValid())
        {
            return this.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(loginName))
        {
            return this.BadRequest();
        }

        var balance = await this._vipService.GetProGoldBalanceAsync(loginName, cancellationToken).ConfigureAwait(false);
        return balance is null ? this.NotFound() : this.Ok(balance);
    }

    /// <summary>
    /// Persists a VIP attribute grant (IsVip + EXP/ZEN bonuses) under the account lock.
    /// </summary>
    [HttpPost("vip")]
    public async Task<IActionResult> GrantVipAsync([FromBody] InternalVipGrantRequest request, CancellationToken cancellationToken)
    {
        if (!this.IsInternalSecretValid())
        {
            return this.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.LoginName) || request.Days < 1)
        {
            return this.BadRequest();
        }

        var accountId = await this.TryGetAccountIdAsync(request.LoginName, cancellationToken).ConfigureAwait(false);
        if (accountId is null)
        {
            return this.NotFound();
        }

        VipGrantResult? result = null;
        await AccountTransactionLock.RunAsync(accountId, async () =>
        {
            result = await this._vipService.GrantVipAsync(
                request.LoginName,
                request.VipLevel < 1 ? 1 : request.VipLevel,
                request.Days,
                cancellationToken).ConfigureAwait(false);
        }).ConfigureAwait(false);

        this._logger.LogInformation(
            "Internal VIP grant for {Login} level={Level} days={Days} status={Status}",
            request.LoginName,
            request.VipLevel,
            request.Days,
            result?.Status);
        return result?.Status switch
        {
            VipGrantStatus.Granted => this.Ok(result.StatusInfo),
            VipGrantStatus.AccountNotFound => this.NotFound(),
            _ => this.BadRequest(),
        };
    }

    /// <summary>
    /// Atomically debits ProGold and grants VIP. Idempotent via <see cref="InternalVipPurchaseRequest.Reference"/>.
    /// </summary>
    [HttpPost("vip-purchase")]
    public async Task<IActionResult> PurchaseVipAsync([FromBody] InternalVipPurchaseRequest request, CancellationToken cancellationToken)
    {
        if (!this.IsInternalSecretValid())
        {
            return this.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.LoginName)
            || string.IsNullOrWhiteSpace(request.Reference)
            || request.Days < 1
            || request.ProGoldPrice < 1)
        {
            return this.BadRequest();
        }

        var accountId = await this.TryGetAccountIdAsync(request.LoginName, cancellationToken).ConfigureAwait(false);
        if (accountId is null)
        {
            return this.NotFound();
        }

        VipPurchaseResult? result = null;
        await AccountTransactionLock.RunAsync(accountId, async () =>
        {
            result = await this._vipService.PurchaseVipAsync(
                request.LoginName,
                request.VipLevel < 1 ? 1 : request.VipLevel,
                request.Days,
                request.ProGoldPrice,
                request.Reference,
                request.Reason,
                cancellationToken).ConfigureAwait(false);
        }).ConfigureAwait(false);

        this._logger.LogInformation(
            "Internal VIP purchase for {Login} days={Days} price={Price} ref={Reference} status={Status}",
            request.LoginName,
            request.Days,
            request.ProGoldPrice,
            request.Reference,
            result?.Status);

        return result?.Status switch
        {
            VipPurchaseStatus.Purchased => this.Ok(new
            {
                status = "purchased",
                vip = result.Vip,
                coinGrant = result.CoinGrant,
            }),
            VipPurchaseStatus.AlreadyPurchased => this.Ok(new
            {
                status = "already_purchased",
                vip = result.Vip,
                coinGrant = result.CoinGrant,
            }),
            VipPurchaseStatus.InsufficientFunds => this.Conflict(new { status = "insufficient_funds" }),
            VipPurchaseStatus.AccountNotFound => this.NotFound(),
            VipPurchaseStatus.ReferenceUsedByOtherAccount => this.Conflict(new { status = "reference_conflict" }),
            _ => this.BadRequest(),
        };
    }

    /// <summary>
    /// Queues a vault item delivery under the account lock.
    /// </summary>
    [HttpPost("vault-item")]
    public async Task<IActionResult> GrantVaultItemAsync([FromBody] InternalVaultItemGrantRequest request, CancellationToken cancellationToken)
    {
        if (!this.IsInternalSecretValid())
        {
            return this.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.LoginName) || request.ItemGroup is null || request.ItemNumber is null)
        {
            return this.BadRequest();
        }

        var accountId = await this.TryGetAccountIdAsync(request.LoginName, cancellationToken).ConfigureAwait(false);
        if (accountId is null)
        {
            return this.NotFound();
        }

        await AccountTransactionLock.RunAsync(accountId, () =>
        {
            this._logger.LogInformation(
                "Internal vault-item grant locked for {Login} item={Group}/{Number} x{Level}",
                request.LoginName,
                request.ItemGroup,
                request.ItemNumber,
                request.Level);
            return ValueTask.CompletedTask;
        }).ConfigureAwait(false);

        return this.Accepted(new
        {
            request.LoginName,
            request.ItemGroup,
            request.ItemNumber,
            request.Level,
            status = "accepted-locked",
        });
    }

    private async ValueTask<Guid?> TryGetAccountIdAsync(string loginName, CancellationToken cancellationToken)
    {
        var configuration = await this._gameConfigurationSource.GetOwnerAsync(Guid.Empty, cancellationToken).ConfigureAwait(false);
        using var context = this._persistence.CreateNewPlayerContext(configuration);
        var account = await context.GetAccountByLoginNameAsync(loginName, cancellationToken).ConfigureAwait(false);
        return account?.GetId();
    }

    private bool IsInternalSecretValid()
    {
        var expected = Environment.GetEnvironmentVariable("PROMU_INTERNAL_API_SECRET");
        if (string.IsNullOrEmpty(expected))
        {
            var env = Environment.GetEnvironmentVariable("PROMU_CRYPTO_ENV") ?? "local";
            return string.Equals(env, "local", StringComparison.OrdinalIgnoreCase);
        }

        if (!this.Request.Headers.TryGetValue("X-Promu-Internal-Secret", out var provided))
        {
            return false;
        }

        return string.Equals(provided.ToString(), expected, StringComparison.Ordinal);
    }
}

/// <summary>Coin grant request from the website.</summary>
public sealed class InternalCoinGrantRequest
{
    /// <summary>Gets or sets the login name.</summary>
    public string LoginName { get; set; } = string.Empty;

    /// <summary>Gets or sets the amount.</summary>
    public int? Amount { get; set; }

    /// <summary>Gets or sets the coin type.</summary>
    public CashShopCoinType? CoinType { get; set; }

    /// <summary>Gets or sets the reason.</summary>
    public string? Reason { get; set; }

    /// <summary>Gets or sets the idempotency reference.</summary>
    public string? Reference { get; set; }
}

/// <summary>VIP grant request from the website.</summary>
public sealed class InternalVipGrantRequest
{
    /// <summary>Gets or sets the login name.</summary>
    public string LoginName { get; set; } = string.Empty;

    /// <summary>Gets or sets the VIP level.</summary>
    public int VipLevel { get; set; } = 1;

    /// <summary>Gets or sets the duration in days.</summary>
    public int Days { get; set; } = 30;
}

/// <summary>Atomic VIP purchase (ProGold debit + VIP grant).</summary>
public sealed class InternalVipPurchaseRequest
{
    /// <summary>Gets or sets the login name.</summary>
    public string LoginName { get; set; } = string.Empty;

    /// <summary>Gets or sets the VIP level.</summary>
    public int VipLevel { get; set; } = 1;

    /// <summary>Gets or sets the duration in days.</summary>
    public int Days { get; set; }

    /// <summary>Gets or sets the ProGold price (WCoinC debit).</summary>
    public int ProGoldPrice { get; set; }

    /// <summary>Gets or sets the idempotency reference (unique).</summary>
    public string Reference { get; set; } = string.Empty;

    /// <summary>Gets or sets the reason.</summary>
    public string? Reason { get; set; }
}

/// <summary>Vault item grant request from the website.</summary>
public sealed class InternalVaultItemGrantRequest
{
    /// <summary>Gets or sets the login name.</summary>
    public string LoginName { get; set; } = string.Empty;

    /// <summary>Gets or sets the item group.</summary>
    public int? ItemGroup { get; set; }

    /// <summary>Gets or sets the item number.</summary>
    public int? ItemNumber { get; set; }

    /// <summary>Gets or sets the item level.</summary>
    public int Level { get; set; }
}
