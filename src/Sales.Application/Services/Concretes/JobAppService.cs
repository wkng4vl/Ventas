﻿using System;
using System.Linq;
using System.Threading.Tasks;

using Abp.Application.Services;
using Abp.Domain.Repositories;
using Abp.Domain.Uow;
using Abp.ObjectMapping;

using Sales.Domain.Entities.Invoices;
using Sales.Domain.Entities.Orders;
using Sales.Domain.Entities.Plans;
using Sales.Domain.Entities.Subscriptions;
using Sales.Domain.PaymentProviders;
using Sales.Domain.Repositories;
using Sales.Domain.Services.Abstracts;

namespace Sales.Application.Services.Concretes
{
    public class JobAppService : ApplicationService
    {
        private readonly ISubscriptionRepository _subscriptionRepository;
        private readonly ISubscriptionCycleRepository _subscriptionCycleRepository;
        private readonly IRepository<SubscriptionCycleOrder, Guid> _subscriptionCycleOrderRepository;
        private readonly IRepository<Plan, Guid> _planRepository;
        private readonly IPlanPriceRepository _planPriceRepository;
        private readonly IOrderRepository _orderRepository;
        private readonly IRepository<Invoice, Guid> _invoiceRepository;
        private readonly IRepository<InvoicePaymentProvider, Guid> _invoicePaymentProviderRepository;
        private readonly IOrderDomainService _orderDomainService;
        private readonly ISubscriptionDomainService _subscriptionDomainService;
        private readonly ISubscriptionCycleDomainService _subscriptionCycleDomainService;
        private readonly ISubscriptionCycleOrderDomainService _subscriptionCycleOrderDomainService;
        private readonly IInvoiceDomainService _invoiceDomainService;
        private readonly IObjectMapper _mapper;
        private readonly IPaypalService _paypalService;
        private readonly IMobbexService _mobbexService;
        private readonly IUnitOfWorkManager _unitOfWorkManager;

        public JobAppService(ISubscriptionRepository subscriptionRepository,
                             ISubscriptionCycleRepository subscriptionCycleRepository,
                             IRepository<SubscriptionCycleOrder, Guid> subscriptionCycleOrderRepository,
                             IRepository<Plan, Guid> planRepository,
                             IPlanPriceRepository planPriceRepository,
                             IOrderRepository orderRepository,
                             IRepository<Invoice, Guid> invoiceRepository,
                             IRepository<InvoicePaymentProvider, Guid> invoicePaymentProviderRepository,
                             IOrderDomainService orderDomainService,
                             ISubscriptionDomainService subscriptionDomainService,
                             ISubscriptionCycleDomainService subscriptionCycleDomainService,
                             ISubscriptionCycleOrderDomainService subscriptionCycleOrderDomainService,
                             IInvoiceDomainService invoiceDomainService,
                             IObjectMapper mapper,
                             IPaypalService paypalService,
                             IMobbexService mobbexService)
        {
            _subscriptionRepository = subscriptionRepository ?? throw new ArgumentNullException(nameof(subscriptionRepository));
            _subscriptionCycleRepository = subscriptionCycleRepository ?? throw new ArgumentNullException(nameof(subscriptionCycleRepository));
            _subscriptionCycleOrderRepository = subscriptionCycleOrderRepository ?? throw new ArgumentNullException(nameof(subscriptionCycleOrderRepository));
            _planRepository = planRepository ?? throw new ArgumentNullException(nameof(planRepository));
            _planPriceRepository = planPriceRepository ?? throw new ArgumentNullException(nameof(planPriceRepository));
            _orderRepository = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));
            _invoiceRepository = invoiceRepository ?? throw new ArgumentNullException(nameof(invoiceRepository));
            _invoicePaymentProviderRepository = invoicePaymentProviderRepository ?? throw new ArgumentNullException(nameof(invoicePaymentProviderRepository));
            _orderDomainService = orderDomainService ?? throw new ArgumentNullException(nameof(orderDomainService));
            _subscriptionDomainService = subscriptionDomainService ?? throw new ArgumentNullException(nameof(subscriptionDomainService));
            _subscriptionCycleDomainService = subscriptionCycleDomainService ?? throw new ArgumentNullException(nameof(subscriptionCycleDomainService));
            _subscriptionCycleOrderDomainService = subscriptionCycleOrderDomainService ?? throw new ArgumentNullException(nameof(subscriptionCycleOrderDomainService));
            _invoiceDomainService = invoiceDomainService ?? throw new ArgumentNullException(nameof(invoiceDomainService));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _paypalService = paypalService ?? throw new ArgumentNullException(nameof(paypalService));
            _mobbexService = mobbexService ?? throw new ArgumentNullException(nameof(mobbexService));
        }

        public async Task CreateNewSubscriptionCycles()
        {
            var subscriptionCycles = _subscriptionCycleRepository.GetSoonExpires(DateTime.Now, 19);

            foreach (var cycle in subscriptionCycles)
            {
                using var unitOfWork = UnitOfWorkManager.Begin();

                SubscriptionCycle newCycle = _subscriptionCycleDomainService.CreateSubscriptionCycle(cycle.Subscription, DateTime.Now);
                _subscriptionCycleDomainService.ActiveSubscriptionCycle(newCycle, cycle.ExpirationDate.Value, cycle.Subscription.Plan.Duration);

                _subscriptionCycleRepository.Insert(newCycle);


                Order oldOrder = _orderRepository.GetBySubscriptionCycle(cycle);

                PlanPrice planPrice = cycle.Subscription.Plan.PlanPrices.Single(x => x.Currency.Code == oldOrder.Currency.Code);

                Order order = _orderDomainService.CreateOrderForRenewSubscription(planPrice, oldOrder.UserId, DateTime.Now);
                _orderDomainService.PaymentPendingOrder(order);
                order = _orderRepository.Insert(order);



                SubscriptionCycleOrder subscriptionCycleOrder = _subscriptionCycleOrderDomainService.CreateSubscriptionCycleOrder(newCycle, order);

                _subscriptionCycleOrderRepository.Insert(subscriptionCycleOrder);

                Invoice invoice = _invoiceDomainService.CreateInvoice(order, DateTime.Now);
                _invoiceDomainService.ActiveInvoice(invoice);
                _invoiceRepository.Insert(invoice);



                InvoicePaymentProvider invoicePaymentProvider = planPrice.Currency.Code switch
                {
                    Domain.ValueObjects.Currency.CurrencyValue.USD => await _paypalService.CreateUriForPayment(invoice, order, cycle.Subscription.Plan),
                    Domain.ValueObjects.Currency.CurrencyValue.ARS => await _mobbexService.CreateUriForPayment(invoice, order, cycle.Subscription.Plan),
                    _ => throw new NotImplementedException()
                };

                _invoicePaymentProviderRepository.Insert(invoicePaymentProvider);

                unitOfWork.Complete();
            }
        }
    }
}
