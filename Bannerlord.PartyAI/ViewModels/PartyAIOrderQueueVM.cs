using Bannerlord.PartyAI.Domain.Models;
using Bannerlord.PartyAI.Models;
using Bannerlord.PartyAI.ViewModels.Components;
using Bannerlord.PartyAI.ViewModels.Dialogs;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Bannerlord.PartyAI.ViewModels;

public class PartyAIOrderQueueVM : ViewModel
{
    private readonly PartyAiEntitySettings _settings;
    private MBBindingList<PartyAIOrderItemVM> _orderList = null!;
    private readonly Action _callback;

    public PartyAIOrderQueueVM(PartyAiEntitySettings settings, Action callback)
    {
        _settings = settings;
        _callback = callback;
        TitleText = _settings.Hero is Hero hero
            ? new TextObject("{=PAI4eHNvDEM}Order Queue for {HERO}'s party")
                .SetTextVariable("HERO", hero.Name)
                .ToString()
            : new TextObject("{=PAI_ORDER_QUEUE}Order Queue").ToString();
        OrderList = new MBBindingList<PartyAIOrderItemVM>();

        RefreshOrderQueue();
        RefreshValues();
    }

    [DataSourceProperty]
    public MBBindingList<PartyAIOrderItemVM> OrderList
    {
        get
        {
            return _orderList;
        }
        set
        {
            if (value != _orderList)
            {
                _orderList = value;
                OnPropertyChangedWithValue(value, "OrderList");
            }
        }
    }

    [DataSourceProperty] public string AcceptText => GameTexts.FindText("str_done").ToString();

    [DataSourceProperty] public string TitleText { get; private set; }
    [DataSourceProperty] public string AddOrderText => new TextObject("{=PAI9PHY91SP}Add Order").ToString();
    [DataSourceProperty] public string ClearQueueText => new TextObject("{=PAIl7GEAaaD}Clear Queue").ToString();

    private void RefreshOrderQueue()
    {
        OrderList.Clear();
        if (_settings.HasActiveOrder)
        {
            OrderList.Add(new(_settings.Order, _settings, RefreshOrderQueue));
            foreach (PartyAiOrder order in _settings.OrderQueue)
            {
                OrderList.Add(new(order, _settings, RefreshOrderQueue));
            }
        }
        else
        {
            OrderList.Add(new(new(PartyAiOrderType.None), _settings, RefreshOrderQueue));
        }
        OnPropertyChanged("OrderList");
    }

    public void AddOrder() => CreateOrder.Create(_settings, RefreshOrderQueue);

    public void ClearQueue()
    {
        string title = new TextObject("{=PAIv8ekJ4gs}Are you sure?").ToString();
        InformationManager.ShowInquiry(new(title, string.Empty, true, true, GameTexts.FindText("str_yes").ToString(), GameTexts.FindText("str_cancel").ToString(), () =>
        {
            _settings.ClearAllOrders();
            RefreshOrderQueue();
        }, null));
    }

    public void DoneOrderQueue()
    {
        _callback.Invoke();
    }
}
