import { apiFetch } from "./client";

export type HitKind = "Client" | "ServiceFile" | "TaxRegistration";

export interface SearchHit {
  kind: HitKind;
  id: string;
  clientId: string | null;
  code: string;
  label: string;
  context: string | null;
  matchedOn: string;
}

export interface SearchResponse {
  total: number;
  skip: number;
  take: number;
  query: string;
  items: SearchHit[];
}

export const searchApi = {
  search: (q: string, opts: { kind?: HitKind; skip?: number; take?: number } = {}) => {
    const p = new URLSearchParams({ q });
    if (opts.kind) p.set("kind", opts.kind);
    if (opts.skip !== undefined) p.set("skip", String(opts.skip));
    if (opts.take !== undefined) p.set("take", String(opts.take));
    return apiFetch<SearchResponse>(`/api/v1/search?${p}`);
  },
};
