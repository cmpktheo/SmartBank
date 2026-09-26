import { defineConfig } from 'vitest/config';
import { BaseSequencer } from 'vitest/node';
import type { TestSpecification } from 'vitest/node';

// Logical test categories, in dependency order:
// 1. Core utilities (pure functions, no DI)
// 2. Core interceptors (cross-cutting HTTP behaviour)
// 3. Auth feature (store + guard — foundation for the rest)
// 4. Accounts & ledger (data-access + page)
// 5. Transfers feature page
// 6. Cards feature page
// NOTE: `include` order alone does not control execution order — Vitest's
// default sequencer re-sorts by duration/cache. CategorySequencer enforces
// the per-category order below.
const CATEGORY_RANK: Array<[RegExp, number]> = [
  [/src\/app\/core\/currency\.spec\.ts$/, 10],
  [/src\/app\/core\/iban\.spec\.ts$/, 11],
  [/src\/app\/core\/interceptors\/auth\.interceptor\.spec\.ts$/, 20],
  [/src\/app\/core\/interceptors\/error\.interceptor\.spec\.ts$/, 21],
  [/src\/app\/features\/auth\/auth\.store\.spec\.ts$/, 30],
  [/src\/app\/features\/auth\/auth\.guard\.spec\.ts$/, 31],
  [/src\/app\/features\/accounts\/data-access\/ledger\.service\.spec\.ts$/, 40],
  [/src\/app\/features\/accounts\/account-detail\.page\.spec\.ts$/, 41],
  [/src\/app\/features\/transfers\/transfer\.page\.spec\.ts$/, 50],
  [/src\/app\/features\/cards\/cards\.page\.spec\.ts$/, 60],
];

function categoryRank(moduleId: string): number {
  const normalized = moduleId.replace(/\\/g, '/');
  for (const [pattern, rank] of CATEGORY_RANK) {
    if (pattern.test(normalized)) return rank;
  }
  return 99;
}

class CategorySequencer extends BaseSequencer {
  async sort(files: TestSpecification[]): Promise<TestSpecification[]> {
    return [...files].sort(
      (a, b) => categoryRank(a.moduleId) - categoryRank(b.moduleId) || a.moduleId.localeCompare(b.moduleId)
    );
  }
}

export default defineConfig({
  test: {
    include: ['src/**/*.spec.ts'],
    exclude: ['e2e/**', 'node_modules/**', 'dist/**'],
    // Keep the logical per-category order: no shuffle, files run sequentially.
    sequence: { shuffle: false, sequencer: CategorySequencer },
    fileParallelism: false,
  },
});
