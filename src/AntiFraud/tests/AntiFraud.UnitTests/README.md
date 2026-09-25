# Testes unitários (xUnit)

Projeto **`AntiFraud.UnitTests`** — regras de domínio e `FraudEvaluationService` (velocity + scores).

## Executar

```bash
cd src/AntiFraud
dotnet test tests/AntiFraud.UnitTests/AntiFraud.UnitTests.csproj
```

Visual Studio: Test Explorer → Run All.

## Cobertura atual

| Área | Testes |
|------|--------|
| `HighAmountRule` | Limiar 10.000 |
| `FraudRuleEngine` | APPROVED, REVIEW (60), BLOCKLIST |
| `FraudEvaluationService` | Idempotência Completed, REVIEW velocity (70), REJECTED (60+70) |
