using Bannerlord.PartyAI.Parties.Orders;
using Bannerlord.PartyAI.Parties.Recruitment;
using Bannerlord.PartyAI.Parties.Templates;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.SaveSystem;

namespace Bannerlord.PartyAI.Parties;

/// <summary>
/// Everything the mod knows about how one party (or garrison) should behave: troop template and
/// composition, behavior permissions, logistics budgets and its order queue.
/// <para>Save ids are part of the save format; never renumber them.</para>
/// </summary>
public class PartyProfile
{
    [SaveableProperty(1)] public Hero? Hero { get; private set; }
    [SaveableProperty(11)] public Settlement? Settlement { get; private set; }

    // ---- Troops ----
    [SaveableProperty(5)] public TroopTemplate? Template { get; private set; }
    [SaveableProperty(6)] public PartyComposition Composition { get; set; }
    [SaveableProperty(15)] public int MaxTroopTier { get; set; }
    [SaveableProperty(18)] public bool AllowRecruitment { get; set; } = true;
    [SaveableProperty(28)] public bool RecruitFromEnemySettlements { get; set; }
    [SaveableProperty(22)] public bool AutoRecruitment { get; set; } = true;
    [SaveableProperty(23)] public float AutoRecruitmentPercentage { get; set; } = 0.5f;
    [SaveableProperty(24)] public bool DismissUnwantedTroops { get; set; }
    [SaveableProperty(25)] public float DismissUnwantedTroopsPercentage { get; set; } = 0.8f;
    [SaveableProperty(29)] public SettlementAutomationLevel SettlementAutomation { get; set; } = SettlementAutomationLevel.Full;

    // ---- Behavior permissions ----
    [SaveableProperty(2)] public bool AllowJoinArmies { get; set; } = true;
    [SaveableProperty(10)] public bool AllowSieging { get; set; } = true;
    [SaveableProperty(4)] public bool AllowRaidVillages { get; set; } = true;
    [SaveableProperty(7)] public bool AllowLordPrisoners { get; set; } = true;
    [SaveableProperty(3)] public bool AllowDonateTroops { get; set; } = true;
    [SaveableProperty(26)] public bool AllowTakeTroopsFromSettlement { get; set; }

    // ---- Logistics ----
    [SaveableProperty(12)] public bool BuyHorses { get; set; }
    [SaveableProperty(13)] public int BuyHorsesBudget { get; set; } = 500;
    [SaveableProperty(14)] public int BuyHorsesBudgetToday { get; private set; } = 500;
    [SaveableProperty(16)] public int TroopsConvertibleToday { get; private set; } = 5;
    [SaveableProperty(27)] public float PatrolRadius { get; set; } = 1f;

    // ---- Caravan trade filter ----
    [SaveableProperty(19)] public bool FilterSettlements { get; set; }
    [SaveableProperty(20)] public List<Settlement> FilteredSettlements { get; set; } = new();

    // ---- Orders ----
    [SaveableProperty(8)] public PartyOrder? Order { get; private set; }
    [SaveableProperty(21)] public List<PartyOrder> OrderQueue { get; set; } = new();
    [SaveableProperty(17)] public PartyOrder? FallbackOrder { get; private set; }

    public PartyProfile()
    {
        Composition = PartyComposition.Default;
    }

    public PartyProfile(Hero? hero) : this()
    {
        Hero = hero;
    }

    public PartyProfile(Settlement settlement) : this()
    {
        Settlement = settlement;
    }

    public PartyProfile(PartyProfile cloneFrom)
    {
        Template = cloneFrom.Template;
        Composition = new PartyComposition(cloneFrom.Composition);
        CopyOptionsFrom(cloneFrom);
    }

    public PartyProfile(PartyProfile cloneFrom, Hero? hero) : this(cloneFrom)
    {
        Hero = hero;
    }

    public PartyProfile(PartyProfile cloneFrom, Settlement settlement) : this(cloneFrom)
    {
        Settlement = settlement;
    }

    public bool IsGarrison => Settlement is not null;

    public MobileParty? Party => Hero?.PartyBelongedTo;

    // ---- Orders --------------------------------------------------------------------------

    [MemberNotNullWhen(true, nameof(Order))]
    public bool HasActiveOrder => Order is not null && Order.Behavior != PartyOrderType.None;

    /// <summary>
    /// Makes <paramref name="behavior"/> the active order. A running player order is pushed to the
    /// front of the queue; a running automatic order is discarded.
    /// </summary>
    public void SetOrder(PartyOrderType behavior, IMapPoint? target = null)
    {
        if (IsGarrison)
        {
            return;
        }

        LeaveForeignArmy();
        NormalizeQueue();

        if (HasActiveOrder && Order.IsPlayerOrder)
        {
            OrderQueue.Insert(0, Order);
        }

        OrderQueue.RemoveAll(order => !order.IsPlayerOrder);
        Order = new PartyOrder(behavior, target);
    }

    /// <summary>Appends a player order after the current one, or starts it if idle.</summary>
    public void EnqueueOrder(PartyOrderType behavior, IMapPoint? target = null)
    {
        if (HasActiveOrder)
        {
            OrderQueue.Add(new PartyOrder(behavior, target));
        }
        else
        {
            SetOrder(behavior, target);
        }
    }

    public void SetFallbackOrder(PartyOrderType behavior, IMapPoint? target = null)
    {
        if (!IsGarrison)
        {
            FallbackOrder = new PartyOrder(behavior, target);
        }
    }

    public void ClearFallbackOrder() => FallbackOrder = null;

    /// <summary>Finishes the active order and promotes the next queued one.</summary>
    public void ClearOrder()
    {
        if (IsGarrison)
        {
            return;
        }

        NormalizeQueue();
        Party?.Ai.SetDoNotMakeNewDecisions(false);

        Order = null;
        if (OrderQueue.Count > 0)
        {
            Order = OrderQueue[0];
            OrderQueue.RemoveAt(0);
        }
    }

    public void ClearAllOrders()
    {
        NormalizeQueue();
        OrderQueue.Clear();
        ClearOrder();
    }

    /// <summary>Removes a specific order wherever it sits.</summary>
    public void RemoveOrder(PartyOrder order)
    {
        if (Order == order)
        {
            ClearOrder();
        }
        else
        {
            OrderQueue.Remove(order);
        }
    }

    /// <summary>Moves a queued order one step towards the front (becoming active at index 0).</summary>
    public void PromoteOrder(PartyOrder order)
    {
        int index = OrderQueue.IndexOf(order);
        if (index < 0)
        {
            return;
        }

        OrderQueue.RemoveAt(index);
        if (index == 0)
        {
            SetOrder(order.Behavior, order.Target);
        }
        else
        {
            OrderQueue.Insert(index - 1, order);
        }
    }

    /// <summary>Moves the active order into the queue, or a queued order one step back.</summary>
    public void DemoteOrder(PartyOrder order)
    {
        if (Order == order)
        {
            ClearOrder();
            OrderQueue.Insert(0, order);
            return;
        }

        int index = OrderQueue.IndexOf(order);
        if (index < 0 || index >= OrderQueue.Count - 1)
        {
            return;
        }

        OrderQueue.RemoveAt(index);
        OrderQueue.Insert(index + 1, order);
    }

    /// <summary>
    /// Suspends the player's orders and runs an automatic (mod-issued) order in their place.
    /// Returns the suspended state so it can be restored later.
    /// </summary>
    internal bool TryBeginAutomaticOrder(
        PartyOrder automaticOrder,
        out PartyOrder? suspendedOrder,
        out List<PartyOrder> suspendedQueue)
    {
        suspendedOrder = null;
        suspendedQueue = new();

        if (IsGarrison
            || automaticOrder.IsPlayerOrder
            || automaticOrder.Behavior == PartyOrderType.None
            || (Order is not null && !Order.IsPlayerOrder))
        {
            return false;
        }

        NormalizeQueue();
        OrderQueue.RemoveAll(order => !order.IsPlayerOrder);

        suspendedOrder = Order is null ? null : new PartyOrder(Order);
        suspendedQueue = OrderQueue.Select(order => new PartyOrder(order)).ToList();

        Order = automaticOrder;
        OrderQueue.Clear();
        UnlockAi();
        return true;
    }

    /// <summary>Restores orders suspended by <see cref="TryBeginAutomaticOrder"/>.</summary>
    internal bool TryRestoreAutomaticOrder(
        int automationToken,
        PartyOrder? suspendedOrder,
        IEnumerable<PartyOrder>? suspendedQueue)
    {
        if (IsGarrison || automationToken <= 0)
        {
            return false;
        }

        NormalizeQueue();
        if (Order is not null && Order.AutomationToken != automationToken)
        {
            OrderQueue.RemoveAll(order => order.AutomationToken == automationToken);
            return false;
        }

        List<PartyOrder> queuedMeanwhile = OrderQueue
            .Where(order => order.IsPlayerOrder)
            .Select(order => new PartyOrder(order))
            .ToList();

        Order = suspendedOrder is { IsPlayerOrder: true } ? new PartyOrder(suspendedOrder) : null;
        OrderQueue = suspendedQueue?
            .Where(order => order is { IsPlayerOrder: true })
            .Select(order => new PartyOrder(order))
            .ToList() ?? new();
        OrderQueue.AddRange(queuedMeanwhile);
        UnlockAi();
        return true;
    }

    /// <summary>Drops an automatic order without restoring anything.</summary>
    internal void AbandonAutomaticOrder(int automationToken)
    {
        if (automationToken <= 0)
        {
            return;
        }

        NormalizeQueue();
        if (Order?.AutomationToken == automationToken)
        {
            Order = null;
        }

        OrderQueue.RemoveAll(order => order.AutomationToken == automationToken);
        UnlockAi();
    }

    private void LeaveForeignArmy()
    {
        MobileParty? party = Party;
        Hero? armyLeader = party?.Army?.LeaderParty.LeaderHero;
        // Whether the player's own party stays in an army is the player's call, never the order system's.
        if (party?.Army is not null && !party.IsMainParty && armyLeader != Hero && armyLeader != TaleWorlds.CampaignSystem.Hero.MainHero)
        {
            party.Army = null;
        }
    }

    private void NormalizeQueue()
    {
        OrderQueue ??= new();
        OrderQueue.RemoveAll(order => order is null);
    }

    private void UnlockAi()
    {
        MobilePartyAi? ai = Party?.Ai;
        if (ai is null)
        {
            return;
        }

        ai.SetDoNotMakeNewDecisions(false);
        ai.RethinkAtNextHourlyTick = true;
    }

    // ---- Template --------------------------------------------------------------------------

    public void SetTemplate(TroopTemplate? template)
    {
        Template = template;
        if (template?.RecommendedComposition is not null)
        {
            Composition = new PartyComposition(template.RecommendedComposition);
        }

        Composition.ApplyTemplate(template);

        // A recruit order aimed at a settlement whose culture the new template cannot use must re-target.
        if (HasActiveOrder
            && Order.Behavior == PartyOrderType.RecruitFromTemplate
            && template is not null)
        {
            bool targetStillValid = Order.Target is Settlement settlement
                && (template.TroopCultures.Count == 0 || template.TroopCultures.Contains(settlement.Culture));
            if (!targetStillValid)
            {
                Order.Target = null;
            }
        }

        UnlockAi();
    }

    // ---- Budgets -------------------------------------------------------------------------

    public void ResetBudgets(int troopsConvertedPerDay)
    {
        BuyHorsesBudgetToday = BuyHorsesBudget;
        TroopsConvertibleToday = troopsConvertedPerDay > 0 ? troopsConvertedPerDay : int.MaxValue;
    }

    public void DeductHorseBudget(int amount) => BuyHorsesBudgetToday -= amount;

    public void DeductTroopsConvertibleToday(int amount) => TroopsConvertibleToday -= amount;

    // ---- Copying -------------------------------------------------------------------------

    /// <summary>Copies every editable option except identity and the active order.</summary>
    public void CopyOptionsFrom(PartyProfile source)
    {
        AllowJoinArmies = source.AllowJoinArmies;
        AllowDonateTroops = source.AllowDonateTroops;
        AllowTakeTroopsFromSettlement = source.AllowTakeTroopsFromSettlement;
        AllowSieging = source.AllowSieging;
        AllowRaidVillages = source.AllowRaidVillages;
        AllowLordPrisoners = source.AllowLordPrisoners;
        BuyHorses = source.BuyHorses;
        BuyHorsesBudget = source.BuyHorsesBudget;
        Composition = new PartyComposition(source.Composition);
        MaxTroopTier = source.MaxTroopTier;
        AllowRecruitment = source.AllowRecruitment;
        RecruitFromEnemySettlements = source.RecruitFromEnemySettlements;
        FilterSettlements = source.FilterSettlements;
        FilteredSettlements = source.FilteredSettlements?.ToList() ?? new();
        OrderQueue = source.OrderQueue?
            .Where(order => order is { IsPlayerOrder: true })
            .Select(order => new PartyOrder(order))
            .ToList() ?? new();
        AutoRecruitment = source.AutoRecruitment;
        AutoRecruitmentPercentage = source.AutoRecruitmentPercentage;
        DismissUnwantedTroops = source.DismissUnwantedTroops;
        DismissUnwantedTroopsPercentage = source.DismissUnwantedTroopsPercentage;
        PatrolRadius = source.PatrolRadius;
        SettlementAutomation = source.SettlementAutomation;

        FallbackOrder = source.FallbackOrder is null || IsGarrison
            ? null
            : new PartyOrder(source.FallbackOrder);

        ResetBudgets(PartyAi.Settings.TroopsConvertedPerDay);
    }

    /// <summary>Replaces this profile's order state with a copy of another profile's.</summary>
    public void CopyOrdersFrom(PartyProfile source)
    {
        ClearAllOrders();
        if (source.HasActiveOrder)
        {
            SetOrder(source.Order.Behavior, source.Order.Target);
        }

        foreach (PartyOrder queued in source.OrderQueue.Where(order => order.IsPlayerOrder))
        {
            OrderQueue.Add(new PartyOrder(queued));
        }
    }
}
