from __future__ import annotations

import random
import time
from collections import defaultdict, deque
from dataclasses import dataclass, field
from typing import Callable, Dict, List, Optional, Tuple


# =========================================================
# 01) Multi-tenant
# =========================================================
@dataclass
class TenantContext:
    tenant_id: str
    plan: str = "standard"


class TenantResolver:
    """Risoluzione tenant per host -> header -> claim."""

    @staticmethod
    def resolve(host: Optional[str], header: Optional[str], claim: Optional[str]) -> TenantContext:
        if host and "." in host:
            return TenantContext(tenant_id=host.split(".")[0])
        if header:
            return TenantContext(tenant_id=header)
        if claim:
            return TenantContext(tenant_id=claim)
        raise ValueError("Tenant non risolto")


@dataclass
class Order:
    order_id: int
    tenant_id: str
    amount: float


class TenantAwareRepository:
    def __init__(self) -> None:
        self._orders: List[Order] = []

    def add_order(self, tenant: TenantContext, order_id: int, amount: float) -> None:
        self._orders.append(Order(order_id=order_id, tenant_id=tenant.tenant_id, amount=amount))

    def list_orders(self, tenant: TenantContext) -> List[Order]:
        return [o for o in self._orders if o.tenant_id == tenant.tenant_id]


# =========================================================
# 02) CQRS + Outbox + Inbox
# =========================================================
@dataclass
class OutboxMessage:
    message_id: int
    event_type: str
    payload: dict
    attempts: int = 0
    processed: bool = False


class Outbox:
    def __init__(self) -> None:
        self._messages: deque[OutboxMessage] = deque()
        self._seq = 1

    def add(self, event_type: str, payload: dict) -> None:
        self._messages.append(OutboxMessage(self._seq, event_type, payload))
        self._seq += 1

    def pending(self) -> List[OutboxMessage]:
        return [m for m in self._messages if not m.processed]


class Inbox:
    def __init__(self) -> None:
        self._processed_keys: set[str] = set()

    def process_once(self, key: str, handler: Callable[[], None]) -> bool:
        if key in self._processed_keys:
            return False
        handler()
        self._processed_keys.add(key)
        return True


class Dispatcher:
    def __init__(self, outbox: Outbox, dead_letter: List[OutboxMessage]) -> None:
        self.outbox = outbox
        self.dead_letter = dead_letter

    def dispatch(self, publish: Callable[[OutboxMessage], None], max_attempts: int = 3) -> None:
        for msg in self.outbox.pending():
            try:
                publish(msg)
                msg.processed = True
            except Exception:
                msg.attempts += 1
                if msg.attempts >= max_attempts:
                    msg.processed = True
                    self.dead_letter.append(msg)


# =========================================================
# 03) AuthN/AuthZ hardening
# =========================================================
@dataclass
class Token:
    jti: str
    user_id: str
    tenant_id: str
    roles: List[str]
    scope: List[str]


class TokenGuardService:
    def __init__(self) -> None:
        self._revoked: set[str] = set()

    def revoke(self, jti: str) -> None:
        self._revoked.add(jti)

    def is_revoked(self, jti: str) -> bool:
        return jti in self._revoked


class AuthorizationService:
    @staticmethod
    def can_manage_billing(token: Token, request_tenant: str) -> bool:
        return (
            token.tenant_id == request_tenant
            and any(r in token.roles for r in ["Admin", "BillingManager"])
            and "billing:write" in token.scope
        )


# =========================================================
# 04) GraphQL persisted query + cost guard + cache
# =========================================================
class GraphQLGateway:
    def __init__(self) -> None:
        self.persisted_queries = {"q1": "orders { id amount }"}
        self.cache: Dict[str, dict] = {}

    def _cache_key(self, tenant_id: str, query_id: str) -> str:
        return f"gql:{tenant_id}:{query_id}"

    def execute(self, tenant_id: str, query_id: str, estimated_cost: int, max_cost: int = 100) -> dict:
        if query_id not in self.persisted_queries:
            raise ValueError("Persisted query obbligatoria")
        if estimated_cost > max_cost:
            raise ValueError("Query oltre budget costo")

        key = self._cache_key(tenant_id, query_id)
        if key in self.cache:
            return {"source": "cache", "data": self.cache[key]}

        data = {"orders": [{"id": 1, "amount": 100.0}]}
        self.cache[key] = data
        return {"source": "live", "data": data}

    def invalidate_tenant(self, tenant_id: str) -> None:
        for key in list(self.cache.keys()):
            if key.startswith(f"gql:{tenant_id}:"):
                del self.cache[key]


# =========================================================
# 05) Observability + SRE
# =========================================================
@dataclass
class Metrics:
    counters: Dict[str, int] = field(default_factory=lambda: defaultdict(int))
    latencies_ms: Dict[str, List[float]] = field(default_factory=lambda: defaultdict(list))

    def inc(self, name: str, amount: int = 1) -> None:
        self.counters[name] += amount

    def observe(self, name: str, value: float) -> None:
        self.latencies_ms[name].append(value)

    def p95(self, name: str) -> float:
        values = sorted(self.latencies_ms[name])
        if not values:
            return 0.0
        idx = int(0.95 * (len(values) - 1))
        return values[idx]


# =========================================================
# 06) Performance benchmark (semplificato)
# =========================================================
class PerformanceBench:
    def __init__(self, metrics: Metrics) -> None:
        self.metrics = metrics

    def run_load(self, requests: int = 300) -> None:
        for _ in range(requests):
            start = time.perf_counter()
            time.sleep(random.uniform(0.001, 0.006))
            elapsed = (time.perf_counter() - start) * 1000
            self.metrics.observe("api.latency", elapsed)
            self.metrics.inc("api.requests")


# =========================================================
# 07) Zero-downtime migration (expand/contract)
# =========================================================
class MigrationService:
    def __init__(self) -> None:
        self.legacy_orders = [{"id": i, "amount": float(i) * 10.0} for i in range(1, 11)]
        self.new_orders: Dict[int, dict] = {}
        self.checkpoint = 0

    def backfill(self, batch_size: int = 3) -> None:
        while self.checkpoint < len(self.legacy_orders):
            end = min(self.checkpoint + batch_size, len(self.legacy_orders))
            batch = self.legacy_orders[self.checkpoint:end]
            for row in batch:
                # idempotente
                self.new_orders[row["id"]] = {"id": row["id"], "total": row["amount"]}
            self.checkpoint = end

    def quality_check(self) -> Tuple[bool, str]:
        if len(self.new_orders) != len(self.legacy_orders):
            return False, "Count mismatch"
        return True, "Data quality OK"


# =========================================================
# 08) Resilience patterns (timeout/retry/circuit breaker/fallback)
# =========================================================
class CircuitBreaker:
    def __init__(self, threshold: int = 3) -> None:
        self.threshold = threshold
        self.failures = 0
        self.open = False

    def call(self, operation: Callable[[], float], fallback: Callable[[], float]) -> float:
        if self.open:
            return fallback()
        try:
            value = operation()
            self.failures = 0
            return value
        except Exception:
            self.failures += 1
            if self.failures >= self.threshold:
                self.open = True
            return fallback()


def retry(operation: Callable[[], float], attempts: int = 3) -> float:
    for i in range(attempts):
        try:
            return operation()
        except Exception:
            if i == attempts - 1:
                raise
            time.sleep(0.01 * (2 ** i))
    raise RuntimeError("unreachable")


# =========================================================
# Programma unico coerente
# =========================================================
def main() -> None:
    print("=== Demo coerente: tutti gli esercizi in un solo programma ===")

    # 01 multi-tenant
    tenant = TenantResolver.resolve(host="acme.api.local", header=None, claim=None)
    repo = TenantAwareRepository()
    repo.add_order(tenant, order_id=1, amount=99.0)
    repo.add_order(TenantContext("globex"), order_id=2, amount=199.0)
    print(f"[01] Ordini tenant {tenant.tenant_id}: {len(repo.list_orders(tenant))}")

    # 02 cqrs/outbox/inbox
    outbox = Outbox()
    dead_letter: List[OutboxMessage] = []
    dispatcher = Dispatcher(outbox, dead_letter)
    inbox = Inbox()

    outbox.add("order.created.v1", {"order_id": 1, "tenant": tenant.tenant_id})

    def flaky_publish(msg: OutboxMessage) -> None:
        if msg.attempts < 1:
            raise RuntimeError("broker down")

    dispatcher.dispatch(flaky_publish)
    dispatcher.dispatch(flaky_publish)
    processed = inbox.process_once("event-1", lambda: print("[02] Consumer side-effect eseguito"))
    duplicate = inbox.process_once("event-1", lambda: print("non deve apparire"))
    print(f"[02] Inbox idempotenza OK={processed and not duplicate}, DLQ={len(dead_letter)}")

    # 03 auth/authz
    token_guard = TokenGuardService()
    token = Token("jti-1", "u1", tenant.tenant_id, ["Admin"], ["billing:write"])
    can_bill_before = AuthorizationService.can_manage_billing(token, tenant.tenant_id)
    token_guard.revoke(token.jti)
    print(f"[03] CanManageBilling={can_bill_before}, Revoked={token_guard.is_revoked(token.jti)}")

    # 04 graphql
    gql = GraphQLGateway()
    r1 = gql.execute(tenant.tenant_id, query_id="q1", estimated_cost=50)
    r2 = gql.execute(tenant.tenant_id, query_id="q1", estimated_cost=50)
    print(f"[04] GraphQL source first={r1['source']} second={r2['source']}")

    # 05+06 observability + perf
    metrics = Metrics()
    bench = PerformanceBench(metrics)
    bench.run_load(250)
    print(f"[05/06] Requests={metrics.counters['api.requests']} p95={metrics.p95('api.latency'):.2f}ms")

    # 07 migration
    migration = MigrationService()
    migration.backfill(batch_size=4)
    quality_ok, quality_msg = migration.quality_check()
    print(f"[07] Backfill records={len(migration.new_orders)} quality={quality_ok} ({quality_msg})")

    # 08 resilience
    breaker = CircuitBreaker(threshold=2)

    def unstable() -> float:
        raise RuntimeError("dependency failed")

    def fallback() -> float:
        return 42.0

    v1 = breaker.call(lambda: retry(unstable, attempts=2), fallback)
    v2 = breaker.call(lambda: retry(unstable, attempts=2), fallback)
    v3 = breaker.call(lambda: retry(unstable, attempts=2), fallback)
    print(f"[08] Fallback values={v1, v2, v3} breaker_open={breaker.open}")

    print("=== Demo completata con successo ===")


if __name__ == "__main__":
    main()
