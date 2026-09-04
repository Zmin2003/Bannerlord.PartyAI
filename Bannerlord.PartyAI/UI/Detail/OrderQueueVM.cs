using Bannerlord.PartyAI.Core;
using Bannerlord.PartyAI.Parties;
using Bannerlord.PartyAI.Parties.Orders;
using Bannerlord.PartyAI.UI.Dialogs;
using System;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;

namespace Bannerlord.PartyAI.UI.Detail;

/// <summary>One line of the order queue with move/delete controls.</summary>
public sealed class OrderItemVM : ViewModel
{
    private readonly PartyProfile _profile;
    private readonly Action _refresh;

    public OrderItemVM(PartyOrder order, PartyProfile profile, Action refresh, bool isActive)
    {
        Order = order;
        _profile = profile;
        _refresh = refresh;
        IsActive = isActive;
        Text = (isActive ? OrderText.Status(order) : OrderText.Command(order)).ToString();
        int index = profile.OrderQueue.IndexOf(order);
        CanMoveUp = !isActive && !order.IsAutomatic;
        CanMoveDown = order.IsPlayerOrder && (isActive ? profile.OrderQueue.Count > 0 : index < profile.OrderQueue.Count - 1);
        CanDelete = order.IsPlayerOrder;
    }

    public PartyOrder Order { get; }

    [DataSourceProperty] public string Text { get; }
    [DataSourceProperty] public bool IsActive { get; }
    [DataSourceProperty] public bool IsAutomatic => Order.IsAutomatic;
    [DataSourceProperty] public bool CanMoveUp { get; }
    [DataSourceProperty] public bool CanMoveDown { get; }
    [DataSourceProperty] public bool CanDelete { get; }
    [DataSourceProperty] public HintViewModel AutomaticHint => new(L.T("{=PAI_ORDER_AUTOMATIC_HINT}Issued by automatic defense or offense. The previous orders resume when it ends."));
    [DataSourceProperty] public HintViewModel MoveUpHint => new(L.T("{=PAI_ORDER_MOVE_UP}Move up"));
    [DataSourceProperty] public HintViewModel MoveDownHint => new(L.T("{=PAI_ORDER_MOVE_DOWN}Move down"));
    [DataSourceProperty] public HintViewModel DeleteHint => new(L.T("{=PAI_ORDER_DELETE}Remove order"));

    public void ExecuteMoveUp()
    {
        _profile.PromoteOrder(Order);
        _refresh();
    }

    public void ExecuteMoveDown()
    {
        _profile.DemoteOrder(Order);
        _refresh();
    }

    public void ExecuteDelete()
    {
        _profile.RemoveOrder(Order);
        _refresh();
    }
}

/// <summary>Active order, queued orders and the standing fallback order of one party.</summary>
public sealed class OrderQueueVM : ViewModel
{
    private readonly PartyProfile _profile;
    private readonly Action _onChanged;
    private MBBindingList<OrderItemVM> _orders = new();
    private string _fallbackText = string.Empty;
    private bool _hasOrders;

    public OrderQueueVM(PartyProfile profile, Action onChanged)
    {
        _profile = profile;
        _onChanged = onChanged;
        Refresh();
    }

    [DataSourceProperty] public string Title => L.S("{=PAI_ORDERS_HEADER}Orders");
    [DataSourceProperty] public string AddText => L.S("{=PAI9PHY91SP}Add Order");
    [DataSourceProperty] public string ClearText => L.S("{=PAIl7GEAaaD}Clear Queue");
    [DataSourceProperty] public string EmptyText => L.S("{=PAIZZ1tGdbA}No active order");
    [DataSourceProperty] public string FallbackLabel => L.S("{=PAIqGqAFj9G}Fallback Order: ");
    [DataSourceProperty] public HintViewModel FallbackHint => new(L.T("{=PAIDJ1aQnLC}Order to issue when the party is not in an army and has no current order."));
    [DataSourceProperty] public HintViewModel ChangeHint => new(L.T("{=PAIXIv9UgAt}Change"));
    [DataSourceProperty] public HintViewModel ClearFallbackHint => new(L.T("{=PAI_CLEAR_FALLBACK}Remove the fallback order"));

    [DataSourceProperty]
    public MBBindingList<OrderItemVM> Orders
    {
        get => _orders;
        private set
        {
            if (value != _orders)
            {
                _orders = value;
                OnPropertyChangedWithValue(value, nameof(Orders));
            }
        }
    }

    [DataSourceProperty]
    public bool HasOrders
    {
        get => _hasOrders;
        private set
        {
            if (value != _hasOrders)
            {
                _hasOrders = value;
                OnPropertyChangedWithValue(value, nameof(HasOrders));
            }
        }
    }

    [DataSourceProperty]
    public string FallbackText
    {
        get => _fallbackText;
        private set
        {
            if (value != _fallbackText)
            {
                _fallbackText = value;
                OnPropertyChangedWithValue(value, nameof(FallbackText));
            }
        }
    }

    [DataSourceProperty] public bool HasFallback => _profile.FallbackOrder is { Behavior: not PartyOrderType.None };

    public void ExecuteAdd()
        => OrderPicker.Show(_profile, forFallback: false, (type, target) =>
        {
            _profile.EnqueueOrder(type, target);
            Refresh();
        });

    public void ExecuteClear()
    {
        if (!_profile.HasActiveOrder && _profile.OrderQueue.Count == 0)
        {
            return;
        }

        InformationManager.ShowInquiry(new InquiryData(
            L.S("{=PAIv8ekJ4gs}Are you sure?"),
            L.S("{=PAI_CLEAR_ORDERS_PROMPT}Remove the active order and every queued order?"),
            true,
            true,
            L.Game("str_yes"),
            L.Game("str_cancel"),
            () =>
            {
                _profile.ClearAllOrders();
                Refresh();
            },
            null));
    }

    public void ExecuteChangeFallback()
        => OrderPicker.Show(_profile, forFallback: true, (type, target) =>
        {
            if (type == PartyOrderType.None)
            {
                _profile.ClearFallbackOrder();
            }
            else
            {
                _profile.SetFallbackOrder(type, target);
                if (!_profile.HasActiveOrder && _profile.Party?.Army is null)
                {
                    _profile.SetOrder(type, target);
                }
            }

            Refresh();
        });

    public void ExecuteClearFallback()
    {
        _profile.ClearFallbackOrder();
        Refresh();
    }

    public void Refresh()
    {
        var list = new MBBindingList<OrderItemVM>();
        if (_profile.HasActiveOrder)
        {
            list.Add(new OrderItemVM(_profile.Order, _profile, Refresh, isActive: true));
            foreach (PartyOrder queued in _profile.OrderQueue)
            {
                list.Add(new OrderItemVM(queued, _profile, Refresh, isActive: false));
            }
        }

        Orders = list;
        HasOrders = list.Count > 0;
        FallbackText = HasFallback ? OrderText.Command(_profile.FallbackOrder).ToString() : L.S("{=koX9okuG}None");
        OnPropertyChanged(nameof(HasFallback));
        _onChanged();
    }
}
