import sys
import unittest
from pathlib import Path

# Permette di eseguire i test senza trasformare la cartella in package.
sys.path.append(str(Path(__file__).resolve().parent))

from main import (
    AuthorizationService,
    CircuitBreaker,
    GraphQLGateway,
    Inbox,
    MigrationService,
    Outbox,
    TenantAwareRepository,
    TenantContext,
    TenantResolver,
    Token,
    TokenGuardService,
    retry,
)


class CombinedProgramTests(unittest.TestCase):
    def test_tenant_isolation(self):
        repo = TenantAwareRepository()
        a = TenantContext("acme")
        b = TenantContext("globex")
        repo.add_order(a, 1, 10)
        repo.add_order(b, 2, 20)
        self.assertEqual(len(repo.list_orders(a)), 1)
        self.assertEqual(repo.list_orders(a)[0].tenant_id, "acme")

    def test_tenant_resolver_fallback(self):
        self.assertEqual(TenantResolver.resolve("acme.api.local", None, None).tenant_id, "acme")
        self.assertEqual(TenantResolver.resolve(None, "h1", None).tenant_id, "h1")
        self.assertEqual(TenantResolver.resolve(None, None, "c1").tenant_id, "c1")

    def test_outbox_and_inbox_idempotency(self):
        outbox = Outbox()
        outbox.add("event", {"x": 1})
        self.assertEqual(len(outbox.pending()), 1)

        inbox = Inbox()
        hit = []
        first = inbox.process_once("k", lambda: hit.append("done"))
        second = inbox.process_once("k", lambda: hit.append("duplicate"))
        self.assertTrue(first)
        self.assertFalse(second)
        self.assertEqual(hit, ["done"])

    def test_auth_policy_and_revocation(self):
        token = Token("j1", "u1", "acme", ["Admin"], ["billing:write"])
        self.assertTrue(AuthorizationService.can_manage_billing(token, "acme"))

        guard = TokenGuardService()
        guard.revoke("j1")
        self.assertTrue(guard.is_revoked("j1"))

    def test_graphql_cache(self):
        gql = GraphQLGateway()
        r1 = gql.execute("acme", "q1", estimated_cost=10)
        r2 = gql.execute("acme", "q1", estimated_cost=10)
        self.assertEqual(r1["source"], "live")
        self.assertEqual(r2["source"], "cache")

    def test_migration_quality(self):
        migration = MigrationService()
        migration.backfill(batch_size=2)
        ok, _ = migration.quality_check()
        self.assertTrue(ok)

    def test_resilience_breaker(self):
        breaker = CircuitBreaker(threshold=2)

        def unstable():
            raise RuntimeError("fail")

        def fallback():
            return 1.0

        self.assertEqual(breaker.call(lambda: retry(unstable, attempts=2), fallback), 1.0)
        self.assertEqual(breaker.call(lambda: retry(unstable, attempts=2), fallback), 1.0)
        self.assertTrue(breaker.open)


if __name__ == "__main__":
    unittest.main()
