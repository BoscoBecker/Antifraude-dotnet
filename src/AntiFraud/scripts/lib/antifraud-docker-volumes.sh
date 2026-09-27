#!/usr/bin/env bash
# Descoberta/remoção de volumes Postgres/Rabbit (Aspire + compose pgadmin).
# Source: . src/AntiFraud/scripts/lib/antifraud-docker-volumes.sh

ASPIRE_POSTGRES_VOLUME="${ASPIRE_POSTGRES_VOLUME:-antifraud-aspire-postgres-data}"
ASPIRE_RABBIT_VOLUME="${ASPIRE_RABBIT_VOLUME:-antifraud-aspire-rabbitmq-data}"
COMPOSE_PGADMIN_DIR_NAME="${COMPOSE_PGADMIN_DIR_NAME:-pgadmin}"

_antifraud_require_docker() {
  command -v docker >/dev/null 2>&1 || {
    echo "Docker não encontrado no PATH."
    return 1
  }
}

# Containers do compose (my-postgres) — não mexer no reset Aspire.
_antifraud_is_compose_pgadmin_container() {
  local name="${1#/}"
  [[ "$name" == "my-postgres" || "$name" == "my-pgadmin" ]]
}

# Volumes declarados no compose pgadmin (nome típico: pgadmin_postgres_data).
_antifraud_compose_pgadmin_volume_names() {
  local project="${COMPOSE_PROJECT_NAME:-$COMPOSE_PGADMIN_DIR_NAME}"
  printf '%s\n' "${project}_postgres_data" "${project}_pgadmin_data"
}

# Volumes montados em containers que parecem Postgres/Rabbit/pgAdmin Aspire (não compose).
_antifraud_discover_aspire_volumes_from_containers() {
  local id name vol
  while read -r id name; do
    [[ -z "$id" ]] && continue
    _antifraud_is_compose_pgadmin_container "$name" && continue
    case "$name" in
      *postgres*|*rabbit*|*pgadmin*|*rabbitmq*)
        while read -r vol; do
          [[ -n "$vol" ]] && printf '%s\n' "$vol"
        done < <(
          docker inspect -f '{{range .Mounts}}{{if eq .Type "volume"}}{{.Name}}{{"\n"}}{{end}}{{end}}' "$id" 2>/dev/null || true
        )
        ;;
    esac
  done < <(docker ps -a --format '{{.ID}} {{.Names}}' 2>/dev/null || true)
}

# Volumes auto-gerados pelo Aspire (nomes antigos, antes de volume fixo no AppHost).
_antifraud_discover_aspire_volumes_by_name_pattern() {
  local vol
  while read -r vol; do
    [[ -z "$vol" ]] && continue
    case "$vol" in
      antifraud-aspire-*|*AppHost*|*apphost*|*antifraud*) printf '%s\n' "$vol" ;;
      *postgres*data*|*rabbit*data*|*rabbitmq*data*) printf '%s\n' "$vol" ;;
    esac
  done < <(docker volume ls -q 2>/dev/null || true)
}

# Exclui volumes do compose quando listamos padrões largos.
_antifraud_filter_out_compose_volumes() {
  local vol
  while read -r vol; do
    [[ -z "$vol" ]] && continue
    case "$vol" in
      pgadmin_postgres_data|pgadmin_pgadmin_data|*_postgres_data|*_pgadmin_data)
        continue
        ;;
    esac
    printf '%s\n' "$vol"
  done
}

antifraud_collect_aspire_reset_volumes() {
  antifraud_collect_all_aspire_volumes
}

# Todos os volumes ligados ao Aspire (Postgres, Rabbit, pgAdmin, nomes auto-gerados, DCP).
antifraud_collect_all_aspire_volumes() {
  {
    printf '%s\n' "$ASPIRE_POSTGRES_VOLUME" "$ASPIRE_RABBIT_VOLUME"
    _antifraud_discover_aspire_volumes_from_containers
    _antifraud_discover_aspire_volumes_by_name_pattern | _antifraud_filter_out_compose_volumes
    _antifraud_discover_aspire_volumes_broad_name_pattern | _antifraud_filter_out_compose_volumes
    _antifraud_discover_aspire_volumes_from_container_labels
  } | awk 'NF && !seen[$0]++'
}

_antifraud_discover_aspire_volumes_broad_name_pattern() {
  local vol
  while read -r vol; do
    [[ -z "$vol" ]] && continue
    case "$vol" in
      *aspire*|*dcp*|*AppHost*|*apphost*) printf '%s\n' "$vol" ;;
    esac
  done < <(docker volume ls -q 2>/dev/null || true)
}

_antifraud_discover_aspire_volumes_from_container_labels() {
  local id vol name
  while read -r id; do
    [[ -z "$id" ]] && continue
    if docker inspect -f '{{.Name}} {{json .Config.Labels}}' "$id" 2>/dev/null | grep -qiE 'aspire|apphost|microsoft\.developer\.ai|dcp'; then
      name=$(docker inspect -f '{{.Name}}' "$id" 2>/dev/null || echo "")
      _antifraud_is_compose_pgadmin_container "$name" && continue
      while read -r vol; do
        [[ -n "$vol" ]] && printf '%s\n' "$vol"
      done < <(
        docker inspect -f '{{range .Mounts}}{{if eq .Type "volume"}}{{.Name}}{{"\n"}}{{end}}{{end}}' "$id" 2>/dev/null || true
      )
    fi
  done < <(docker ps -aq 2>/dev/null || true)
}

antifraud_stop_containers_using_volumes() {
  local vol id
  for vol in "$@"; do
    [[ -z "$vol" ]] && continue
    while read -r id; do
      [[ -n "$id" ]] && docker rm -f "$id" 2>/dev/null || true
    done < <(docker ps -aq --filter "volume=$vol" 2>/dev/null || true)
  done
}

antifraud_wipe_all_aspire_volumes() {
  local dry_run=${1:-0}
  mapfile -t VOLUMES < <(antifraud_collect_all_aspire_volumes)
  if ((${#VOLUMES[@]} == 0)); then
    echo "Nenhum volume Aspire encontrado."
    return 0
  fi
  echo "Volumes Aspire a apagar (${#VOLUMES[@]}):"
  printf '  - %s\n' "${VOLUMES[@]}"
  if ((dry_run == 1)); then
    return 0
  fi
  antifraud_stop_aspire_infra_containers
  antifraud_stop_containers_using_volumes "${VOLUMES[@]}"
  antifraud_remove_volumes_list "${VOLUMES[@]}"
}

antifraud_collect_compose_pgadmin_volumes() {
  {
    _antifraud_compose_pgadmin_volume_names
    docker volume ls -q 2>/dev/null | grep -E 'postgres_data$|pgadmin_data$' || true
  } | awk 'NF && !seen[$0]++'
}

antifraud_stop_compose_pgadmin_containers() {
  local root=$1
  local compose_dir="$root/src/docker/pgadmin"
  docker compose --project-directory "$compose_dir" -f "$compose_dir/docker-compose.yaml" down --remove-orphans 2>/dev/null || true
}

antifraud_stop_aspire_infra_containers() {
  local id name
  while read -r id name; do
    [[ -z "$id" ]] && continue
    _antifraud_is_compose_pgadmin_container "$name" && continue
    case "$name" in
      *postgres*|*rabbit*|*rabbitmq*|*pgadmin*)
        docker rm -f "$id" 2>/dev/null || true
        ;;
    esac
  done < <(docker ps -aq --format '{{.ID}} {{.Names}}' 2>/dev/null || true)
}

antifraud_remove_volumes_list() {
  local vol err=0
  for vol in "$@"; do
    [[ -z "$vol" ]] && continue
    if docker volume inspect "$vol" >/dev/null 2>&1; then
      if docker volume rm "$vol" 2>/dev/null; then
        echo "Removido: $vol"
      else
        echo "Falha (em uso?): $vol"
        err=1
      fi
    else
      echo "Não existe (ok): $vol"
    fi
  done
  return "$err"
}
