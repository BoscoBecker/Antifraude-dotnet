# ADR 004 — Deployment em containers (Docker/Kubernetes)



## Status



Aceito



## Contexto



API e Worker escalam de forma independente; ambiente local utiliza **Docker Compose** na pasta `docker/`.



## Decisão



Empacotar **AntiFraud.Api** e **AntiFraud.Worker** em imagens OCI, orquestradas via **Kubernetes** (prod) ou **Docker Compose** (dev). PostgreSQL e RabbitMQ como serviços gerenciados ou containers side-by-side.



## Consequências



- HPA no worker baseado em lag da fila.

- API stateless — escala horizontal sem sticky session.

- Migrations/DDL aplicadas via job init ou pipeline, nunca drop automático em produção.

