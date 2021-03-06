﻿
using Abp.Domain.Services;

using Sales.Domain.Entities.Notifications;
using Sales.Domain.Entities.Orders;

namespace Sales.Domain.Services.Abstracts
{
    public interface INotificationDomainService : IDomainService
    {
        Notification CreateNotification(Order order);
        void SetOrderPayed(Notification notification);
        void SetNewSubscribeCycle(Notification notification);
        void AddAttempt(Notification notification);
        void Processing(Notification notification);
        void SetError(Notification notification);
    }
}
