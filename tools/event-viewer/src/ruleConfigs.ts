export interface RuleFilterConfig {
  id: string;
  label: string;
  description: string;
  ruleFilter: string | null;
  chipClass: string;
}

/** Demo rule configurations selectable in the dashboard (Milestone 3 AC-3). */
export const DEMO_RULE_CONFIGS: RuleFilterConfig[] = [
  {
    id: 'all',
    label: 'All Rules',
    description: 'Every matched transaction across all enabled rules',
    ruleFilter: null,
    chipClass: 'default',
  },
  {
    id: 'metadata',
    label: 'Metadata',
    description: 'Transactions carrying on-chain metadata (CIP-20, labels, key/value patterns)',
    ruleFilter: 'metadata-key-value',
    chipClass: 'metadata',
  },
  {
    id: 'governance',
    label: 'Governance & Treasury',
    description: 'Governance votes, treasury withdrawals, and Conway-era actions',
    ruleFilter: 'governance-treasury',
    chipClass: 'governance',
  },
  {
    id: 'address',
    label: 'Address Match',
    description: 'DEX order addresses and wallet address prefix matches',
    ruleFilter: 'address-match',
    chipClass: 'address-match',
  },
];

export function buildStreamUrl(baseUrl: string, ruleFilter: string | null): string {
  if (!ruleFilter) return baseUrl;
  const url = new URL(baseUrl);
  url.searchParams.set('ruleFilter', ruleFilter);
  return url.toString();
}

export function formatMetadataSummary(criteria: Record<string, unknown> | undefined): string {
  if (!criteria || Object.keys(criteria).length === 0) return '—';
  if (typeof criteria.swap_summary === 'string' && criteria.swap_summary.length > 0) {
    return criteria.swap_summary;
  }
  const parts = Object.entries(criteria)
    .filter(([k]) => k !== 'minswap_swap')
    .slice(0, 2)
    .map(([k, v]) => {
      const val = typeof v === 'object' ? JSON.stringify(v) : String(v);
      return `${k}: ${val.length > 24 ? val.slice(0, 24) + '…' : val}`;
    });
  const extra = Object.keys(criteria).length > 2 ? ` +${Object.keys(criteria).length - 2}` : '';
  return parts.join(', ') + extra;
}
