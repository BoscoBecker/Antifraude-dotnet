# Testes k6 — AntiFraud

Pasta de **stress** e **cenários de regras** contra `POST /transactions` e `GET /transactions/{id}`.

**Pré-requisitos:** API + **Worker** no ar (Postgres; Rabbit se `UseRabbitMq: true`).

## Instalar k6

| Plataforma | Comando / link |
|------------|----------------|
| **Windows (winget)** | `winget install Grafana.k6` |
| **Windows (Chocolatey)** | `choco install k6` |
| **Documentação** | [Install k6](https://grafana.com/docs/k6/latest/set-up/install-k6/) |
| **Download binários** | [GitHub — grafana/k6 releases](https://github.com/grafana/k6/releases) |

```bash
k6 version
```

## Scripts

| Arquivo | Objetivo |
|---------|----------|
| **`fraud-rules.js`** | Valida **APPROVED**, **REVIEW** (valor alto / velocity), **REJECTED** (60+70) — 1 VU, sequencial |
| **`stress-mixed.js`** | Rampa de VUs; mix ~85% aprovados + picos de valor alto / clientes “quentes” |
| **`transactions.js`** | Carga só no POST (202/200), sem poll de decisão |

Helpers: **`helpers/antifraud.js`** (submit, poll até `COMPLETED`).

### Regras exercitadas (`fraud-rules.js`)

| Grupo | Condição | Decisão esperada |
|-------|----------|------------------|
| APPROVED | amount 250, cliente único | `APPROVED` |
| REVIEW — HIGH_AMOUNT | amount ≥ 10_000 | `REVIEW` (score 60) |
| REVIEW — VELOCITY | 5× amount 50, mesmo `customerId` | `REVIEW` (score 70 na 5ª) |
| REJECTED | 4× APPROVED + 1× 15_000 mesmo cliente | `REJECTED` (60+70) |

## Executar

Na pasta `src/AntiFraud/tests/k6`:

```bash
# Cenários de fraude (recomendado antes do stress)
k6 run -e BASE_URL=http://localhost:5080 fraud-rules.js

# Stress misto
k6 run -e BASE_URL=http://localhost:5080 stress-mixed.js

# Stress validando decisão (mais lento — poll GET)
k6 run -e BASE_URL=http://localhost:5080 -e VERIFY_DECISION=true stress-mixed.js

# Só throughput POST
k6 run -e BASE_URL=http://localhost:5080 transactions.js
```

Smoke:

```bash
k6 run -e BASE_URL=http://localhost:5080 --vus 1 --duration 5s transactions.js
```

Salvar métricas em `../Results` (crie a pasta se ainda não existir):

```bash
k6 run -e BASE_URL=http://localhost:5080 --out json=../Results/result-transactions.json transactions.js
k6 run -e BASE_URL=http://localhost:5080 --summary-export ../Results/fraud-rules_summary.json fraud-rules.js
```

### Exemplo — `transactions.js` (execução local)

Comando (PowerShell, pasta `tests/k6`):

```powershell
k6 run --out json=../Results/result-transactions.json .\transactions.js
```

Saída resumida (API + Worker no ar; ~2 min, cenário `steady_load`):

```text
     execution: local
        script: .\transactions.js
        output: json (../Results/result-transactions.json)

     scenarios: (100.00%) 1 scenario, 25 max VUs, 2m10s max duration (incl. graceful stop):
              * steady_load: Up to 25 looping VUs for 2m0s over 3 stages (gracefulRampDown: 10s, gracefulStop: 30s)

  █ THRESHOLDS

    http_req_duration
    ✓ 'p(95)<3000' p(95)=219.26ms

    http_req_failed
    ✓ 'rate<0.05' rate=0.00%

  █ TOTAL RESULTS

    checks_total.......: 4415    36.754067/s
    checks_succeeded...: 100.00% 4415 out of 4415
    checks_failed......: 0.00%   0 out of 4415

    ✓ status 202 or 200

    HTTP
    http_req_duration..............: avg=49.7ms   min=5.61ms   med=21.27ms  max=845.2ms p(90)=131.74ms p(95)=219.26ms
      { expected_response:true }...: avg=49.7ms   min=5.61ms   med=21.27ms  max=845.2ms p(90)=131.74ms p(95)=219.26ms
    http_req_failed................: 0.00%  0 out of 4415
    http_reqs......................: 4415   36.754067/s

    EXECUTION
    iteration_duration.............: avg=351.44ms min=305.97ms med=323.06ms max=1.14s   p(90)=432.69ms p(95)=521ms
    iterations.....................: 4415   36.754067/s
    vus............................: 1      min=0         max=25
    vus_max........................: 25     min=25        max=25

    NETWORK
    data_received..................: 1.8 MB 15 kB/s
    data_sent......................: 1.4 MB 12 kB/s

running (2m00.1s), 00/25 VUs, 4415 complete and 0 interrupted iterations
steady_load ✓ [======================================] 00/25 VUs  2m0s
```

### Variáveis de ambiente

| Variável | Default | Descrição |
|----------|---------|-----------|
| `BASE_URL` | `http://localhost:5080` | API |
| `POLL_INTERVAL_SEC` | `0.5` | Intervalo entre GETs no poll |
| `POLL_MAX_ATTEMPTS` | `60` | Tentativas (~30 s) |
| `VERIFY_DECISION` | `false` | `stress-mixed.js` aguarda decisão final |

## Windows (PowerShell)

```powershell
cd D:\Tecnica\src\AntiFraud\tests\k6
k6 run -e BASE_URL=http://localhost:5080 fraud-rules.js
```
