using AntiFraud.Application.Transactions;
using AntiFraud.Domain.Rules;
using Microsoft.Extensions.DependencyInjection;

namespace AntiFraud.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<IFraudRule, HighAmountRule>();
        services.AddSingleton<IFraudRule, VelocityRule>();
        services.AddSingleton<FraudRuleEngine>(sp =>
            new FraudRuleEngine(sp.GetServices<IFraudRule>()));

        services.AddScoped<ITransactionService, TransactionService>();
        services.AddScoped<IFraudEvaluationService, FraudEvaluationService>();

        return services;
    }
}
