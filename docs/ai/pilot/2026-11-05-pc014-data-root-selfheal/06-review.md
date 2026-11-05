# 06 Review（task-12）

1. 修复是机制级还是逐例？机制级——自愈对所有 XCodeTestFixture 消费者生效，不逐类打补丁。
2. 会否引入新竞态？只在「检测到失窃」时动 ConnStrs（每跑程 ≤窃绑事件数次），非 PILOT-051 的逐类换库形态。
3. 与 task-15/17（db-dev）边界？我只动 XCodeTestFixture/AIProviderRegistryTests；DbAdminFixture/静态复位归 db-dev，写面无交集。
4. Final Decision: **APPROVED**（lead 已验收：机制级自愈而非逐类打补丁）。
