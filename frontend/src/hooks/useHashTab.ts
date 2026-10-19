import { useLocation, useNavigate, useSearchParams } from "react-router";

export interface UseHashTabReturn {
  activeTab: string;
  selectTab: (tabId: string) => void;
}

/**
 * URL-hash-driven tab state shared by all tabbed pages. The active tab is the
 * URL hash (without its leading `#`) when it is one of `tabIds`; any other
 * value — including no hash — resolves to `defaultTab`. Selecting a tab updates
 * the hash via client-side push navigation: non-default tabs set `#<id>`, the
 * default tab clears the hash. The current query string is preserved.
 */
export function useHashTab(
  tabIds: readonly string[],
  defaultTab: string,
): UseHashTabReturn {
  const navigate = useNavigate();
  const { hash } = useLocation();
  const [searchParams] = useSearchParams();

  const hashTab = hash.replace(/^#/, "");
  const activeTab = tabIds.includes(hashTab) ? hashTab : defaultTab;

  const selectTab = (tabId: string) => {
    // Toggle only the hash; keep the current path (omit pathname → react-router
    // preserves it) and carry the current query string from searchParams. We
    // deliberately do not feed the useLocation() object into navigate() so the
    // tab control can't become a redirection sink.
    const query = searchParams.toString();
    navigate({
      search: query ? `?${query}` : "",
      hash: tabId === defaultTab ? "" : `#${tabId}`,
    });
  };

  return { activeTab, selectTab };
}
