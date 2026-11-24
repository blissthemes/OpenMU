// <copyright file="VipAccountService.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.Services;

using System.Threading;
using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.Persistence;

/// <summary>
/// Grants and inspects ProMU VIP account attribute grants (IsVip + EXP/ZEN bonuses).
/// </summary>
/// <remarks>
/// ProMU cash brands (no second portal wallet):
/// ProCoin = <see cref="CashShopCoinType.WCoinC"/> (donate / vote),
/// ProGold = <see cref="CashShopCoinType.WCoinP"/> (game premium; VIP debit),
/// ProGoblinPoint = <see cref="CashShopCoinType.GoblinPoints"/> (quests / bosses / activity).
/// Bonus grants use fixed values (do not stack on renew): +0.2 BonusExperienceRate, +0.5 MoneyAmountRate.
/// </remarks>
public sealed class VipAccountService
{
    /// <summary>Bonus experience rate added while VIP is active (+20% monster EXP).</summary>
    public const float VipBonusExperienceRate = 0.2f;

    /// <summary>Money drop amount rate added while VIP is active (+50% ZEN).</summary>
    public const float VipMoneyAmountRateBonus = 0.5f;

    /// <summary>ProCoin — donate / vote (OpenMU WCoinC).</summary>
    public static CashShopCoinType ProCoinType => CashShopCoinType.WCoinC;

    /// <summary>ProGold — game premium currency (OpenMU WCoinP). VIP is priced in ProGold.</summary>
    public static CashShopCoinType ProGoldCoinType => CashShopCoinType.WCoinP;

    /// <summary>ProGoblinPoint — quests / bosses / activity (OpenMU GoblinPoints).</summary>
    public static CashShopCoinType ProGoblinPointType => CashShopCoinType.GoblinPoints;

    private readonly IPersistenceContextProvider _persistenceContextProvider;
    private readonly IDataSource<GameConfiguration> _gameConfigurationSource;
    private readonly CashShopCoinService _coinService;

    /// <summary>
    /// Initializes a new instance of the <see cref="VipAccountService"/> class.
    /// </summary>
    public VipAccountService(
        IPersistenceContextProvider persistenceContextProvider,
        IDataSource<GameConfiguration> gameConfigurationSource)
    {
        this._persistenceContextProvider = persistenceContextProvider;
        this._gameConfigurationSource = gameConfigurationSource;
        this._coinService = new CashShopCoinService(persistenceContextProvider, gameConfigurationSource);
    }

    /// <summary>
    /// Gets the VIP status of an account.
    /// </summary>
    public async ValueTask<VipStatusInfo?> GetStatusAsync(string loginName, CancellationToken cancellationToken = default)
    {
        using var context = await this.CreateContextAsync(cancellationToken).ConfigureAwait(false);
        if (await context.GetAccountByLoginNameAsync(loginName, cancellationToken).ConfigureAwait(false) is not { } account)
        {
            return null;
        }

        var utcNow = DateTime.UtcNow;
        var vipGrant = FindActiveGrant(account, Stats.IsVip.Id, utcNow);
        var validUntil = vipGrant?.ValidUntil;
        var isActive = vipGrant is not null && vipGrant.IsValidAt(utcNow);
        return new VipStatusInfo(
            account.LoginName,
            isActive,
            vipGrant?.Value ?? 0,
            validUntil,
            isActive && validUntil is { } until ? until - utcNow : null);
    }

    /// <summary>
    /// Extends or creates VIP grants for the account (IsVip + EXP + ZEN bonuses).
    /// </summary>
    public async ValueTask<VipGrantResult> GrantVipAsync(
        string loginName,
        int vipLevel,
        int days,
        CancellationToken cancellationToken = default)
    {
        if (days < 1 || vipLevel < 1)
        {
            return new VipGrantResult(VipGrantStatus.InvalidRequest, null);
        }

        using var context = await this.CreateContextAsync(cancellationToken).ConfigureAwait(false);
        if (await context.GetAccountByLoginNameAsync(loginName, cancellationToken).ConfigureAwait(false) is not { } account)
        {
            return new VipGrantResult(VipGrantStatus.AccountNotFound, null);
        }

        var configuration = await this._gameConfigurationSource.GetOwnerAsync(Guid.Empty, cancellationToken).ConfigureAwait(false);
        var validUntil = ApplyOrExtendVipGrants(context, account, configuration, vipLevel, days, DateTime.UtcNow);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new VipGrantResult(VipGrantStatus.Granted, new VipStatusInfo(loginName, true, vipLevel, validUntil, validUntil - DateTime.UtcNow));
    }

    /// <summary>
    /// Atomically debits ProGold (WCoinC) and grants VIP under one persistence context.
    /// Idempotent when <paramref name="reference"/> was already used for this account.
    /// </summary>
    public async ValueTask<VipPurchaseResult> PurchaseVipAsync(
        string loginName,
        int vipLevel,
        int days,
        int proGoldPrice,
        string reference,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        if (days < 1 || vipLevel < 1 || proGoldPrice < 1 || string.IsNullOrWhiteSpace(reference))
        {
            return new VipPurchaseResult(VipPurchaseStatus.InvalidRequest, null, null);
        }

        reference = reference.Trim();

        using var context = await this.CreateContextAsync(cancellationToken).ConfigureAwait(false);
        if (await context.GetAccountByLoginNameAsync(loginName, cancellationToken).ConfigureAwait(false) is not { } account)
        {
            return new VipPurchaseResult(VipPurchaseStatus.AccountNotFound, null, null);
        }

        var accountId = account.GetId();
        if (await context.GetCashShopCoinGrantByReferenceAsync(reference, cancellationToken).ConfigureAwait(false) is { } existingGrant)
        {
            if (existingGrant.AccountId != accountId)
            {
                return new VipPurchaseResult(VipPurchaseStatus.ReferenceUsedByOtherAccount, null, null);
            }

            var existingStatus = await this.GetStatusAsync(loginName, cancellationToken).ConfigureAwait(false);
            return new VipPurchaseResult(
                VipPurchaseStatus.AlreadyPurchased,
                existingStatus,
                CashShopCoinGrantInfo.From(existingGrant));
        }

        var pending = await context.GetPendingCashShopCoinGrantsAsync(accountId, cancellationToken).ConfigureAwait(false);
        var effectiveBalance = account.WCoinP + pending
            .Where(g => g.CoinType == ProGoldCoinType)
            .Sum(g => g.Amount);
        if (effectiveBalance < proGoldPrice)
        {
            return new VipPurchaseResult(VipPurchaseStatus.InsufficientFunds, null, null);
        }

        var configuration = await this._gameConfigurationSource.GetOwnerAsync(Guid.Empty, cancellationToken).ConfigureAwait(false);
        var coinGrant = context.CreateNew<CashShopCoinGrant>();
        coinGrant.AccountId = accountId;
        coinGrant.CoinType = ProGoldCoinType;
        coinGrant.Amount = -proGoldPrice;
        coinGrant.Reason = reason ?? $"VIP {days}d";
        coinGrant.Reference = reference;
        coinGrant.GrantedBy = "website-vip-purchase";
        coinGrant.CreatedAt = DateTime.UtcNow;

        var validUntil = ApplyOrExtendVipGrants(context, account, configuration, vipLevel, days, DateTime.UtcNow);
        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // Concurrent request may have won the unique reference race between check and save.
            using var retryContext = await this.CreateContextAsync(cancellationToken).ConfigureAwait(false);
            if (await retryContext.GetCashShopCoinGrantByReferenceAsync(reference, cancellationToken).ConfigureAwait(false) is { } concurrent)
            {
                if (concurrent.AccountId != accountId)
                {
                    return new VipPurchaseResult(VipPurchaseStatus.ReferenceUsedByOtherAccount, null, null);
                }

                var status = await this.GetStatusAsync(loginName, cancellationToken).ConfigureAwait(false);
                return new VipPurchaseResult(VipPurchaseStatus.AlreadyPurchased, status, CashShopCoinGrantInfo.From(concurrent));
            }

            throw;
        }

        return new VipPurchaseResult(
            VipPurchaseStatus.Purchased,
            new VipStatusInfo(loginName, true, vipLevel, validUntil, validUntil - DateTime.UtcNow),
            CashShopCoinGrantInfo.From(coinGrant));
    }

    /// <summary>
    /// Gets ProGold (WCoinP) effective balance including pending grants.
    /// </summary>
    public async ValueTask<ProGoldBalanceInfo?> GetProGoldBalanceAsync(string loginName, CancellationToken cancellationToken = default)
    {
        var summary = await this._coinService.GetSummaryAsync(loginName, cancellationToken).ConfigureAwait(false);
        if (summary is null)
        {
            return null;
        }

        var pendingDelta = summary.Grants
            .Where(g => g.CoinType == ProGoldCoinType && g.AppliedAt is null)
            .Sum(g => g.Amount);
        return new ProGoldBalanceInfo(summary.LoginName, summary.WCoinP, summary.WCoinP + pendingDelta);
    }

    private static DateTime ApplyOrExtendVipGrants(
        IContext context,
        Account account,
        GameConfiguration configuration,
        int vipLevel,
        int days,
        DateTime utcNow)
    {
        var isVip = RequireAttribute(configuration, Stats.IsVip);
        var bonusExp = RequireAttribute(configuration, Stats.BonusExperienceRate);
        var moneyRate = RequireAttribute(configuration, Stats.MoneyAmountRate);

        var baseUntil = utcNow;
        var existingVip = FindLatestGrant(account, Stats.IsVip.Id);
        if (existingVip is { ValidUntil: { } until } && until > utcNow)
        {
            baseUntil = until;
        }

        var validUntil = baseUntil.AddDays(days);
        UpsertGrant(context, account, isVip, vipLevel, validUntil, utcNow);
        UpsertGrant(context, account, bonusExp, VipBonusExperienceRate, validUntil, utcNow);
        UpsertGrant(context, account, moneyRate, VipMoneyAmountRateBonus, validUntil, utcNow);
        return validUntil;
    }

    private static void UpsertGrant(
        IContext context,
        Account account,
        AttributeDefinition attribute,
        float value,
        DateTime validUntil,
        DateTime utcNow)
    {
        var existing = FindLatestGrant(account, attribute.Id);
        if (existing is not null && existing.IsValidAt(utcNow))
        {
            existing.Value = value;
            existing.ValidUntil = validUntil;
            return;
        }

        var grant = context.CreateNew<AccountAttributeGrant>();
        grant.Attribute = attribute;
        grant.Value = value;
        grant.ValidUntil = validUntil;
        account.AttributeGrants.Add(grant);
    }

    private static AccountAttributeGrant? FindActiveGrant(Account account, Guid attributeId, DateTime utcNow)
        => FindLatestGrant(account, attributeId) is { } grant && grant.IsValidAt(utcNow) ? grant : null;

    private static AccountAttributeGrant? FindLatestGrant(Account account, Guid attributeId)
    {
        return (account.AttributeGrants ?? [])
            .Where(g => g.Attribute?.Id == attributeId)
            .OrderByDescending(g => g.ValidUntil ?? DateTime.MaxValue)
            .FirstOrDefault();
    }

    private static AttributeDefinition RequireAttribute(GameConfiguration configuration, AttributeDefinition template)
    {
        return configuration.Attributes.FirstOrDefault(a => a.Id == template.Id)
            ?? throw new InvalidOperationException($"Attribute {template.Designation} is missing from the game configuration.");
    }

    private async ValueTask<IPlayerContext> CreateContextAsync(CancellationToken cancellationToken)
    {
        var gameConfiguration = await this._gameConfigurationSource.GetOwnerAsync(Guid.Empty, cancellationToken).ConfigureAwait(false);
        return this._persistenceContextProvider.CreateNewPlayerContext(gameConfiguration);
    }
}

/// <summary>VIP status DTO.</summary>
public sealed record VipStatusInfo(
    string LoginName,
    bool IsActive,
    float VipLevel,
    DateTime? ValidUntilUtc,
    TimeSpan? Remaining);

/// <summary>ProGold balance DTO (WCoinP).</summary>
public sealed record ProGoldBalanceInfo(string LoginName, int StoredWCoinP, int EffectiveProGold);

/// <summary>VIP grant result.</summary>
public sealed record VipGrantResult(VipGrantStatus Status, VipStatusInfo? StatusInfo);

/// <summary>VIP purchase result.</summary>
public sealed record VipPurchaseResult(VipPurchaseStatus Status, VipStatusInfo? Vip, CashShopCoinGrantInfo? CoinGrant);

/// <summary>VIP grant status.</summary>
public enum VipGrantStatus
{
    Granted,
    AccountNotFound,
    InvalidRequest,
}

/// <summary>VIP purchase status.</summary>
public enum VipPurchaseStatus
{
    Purchased,
    AlreadyPurchased,
    InsufficientFunds,
    AccountNotFound,
    InvalidRequest,
    ReferenceUsedByOtherAccount,
}
