import { useEffect, useRef, useState } from "react";
import { useLocation } from "react-router";
import { useAuth } from "../auth/AuthContext";
import { fetchStoreSummary } from "../stores/storesApi";
import { fetchWishlistGrouped } from "../wishlist/wishlistApi";

export type SetupStepId = "credential" | "store" | "wishlist";

export type SetupStep = { id: SetupStepId; done: boolean };

export type SetupProgress =
  | { status: "loading" }
  | { status: "error" }
  | {
      status: "ready";
      steps: SetupStep[];
      done: number;
      total: number;
      isComplete: boolean;
      /** The optional step: any wishlist entry has auto-redeem on. Never counts toward `done`. */
      autoRedeem: boolean;
    };

type Counts = { stores: number; wishlistGroups: number; autoRedeem: boolean };

/**
 * First-run progress, derived from what the owner already has rather than from anything they
 * clicked, so it cannot drift from the real state and needs no storage. Recounted on every route
 * change: the steps are completed on other pages, and returning from one should show the tick.
 */
export const useSetupProgress = (): SetupProgress => {
  const { user } = useAuth();
  const { pathname } = useLocation();
  const [counts, setCounts] = useState<Counts | null>(null);
  const [failed, setFailed] = useState(false);
  const requestId = useRef(0);

  useEffect(() => {
    const id = ++requestId.current;
    Promise.all([fetchStoreSummary(), fetchWishlistGrouped()])
      .then(([summary, wishlist]) => {
        if (id !== requestId.current) return;
        setCounts({
          stores: summary.activeCount + summary.pausedCount,
          wishlistGroups: wishlist.items.length,
          autoRedeem: wishlist.items.some((group) =>
            group.entries.some((entry) => entry.autoRedeem),
          ),
        });
        setFailed(false);
      })
      .catch(() => {
        if (id === requestId.current) setFailed(true);
      });
  }, [pathname]);

  // A count that loaded once stays shown through a failed recount.
  if (counts === null)
    return failed ? { status: "error" } : { status: "loading" };

  const steps: SetupStep[] = [
    {
      id: "credential",
      done: (user?.credentials?.length ?? 0) > 0,
    },
    { id: "store", done: counts.stores > 0 },
    { id: "wishlist", done: counts.wishlistGroups > 0 },
  ];
  const done = steps.filter((step) => step.done).length;

  return {
    status: "ready",
    steps,
    done,
    total: steps.length,
    isComplete: done === steps.length,
    autoRedeem: counts.autoRedeem,
  };
};
