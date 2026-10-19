import { useCallback, useEffect, useState } from "react";
import { fetchStoreProfile, type StoreInputProfile } from "./storesApi";

export type StoreProfileDataStatus = "loading" | "idle";

export type StoreProfileData = {
  profile: StoreInputProfile | null;
  profileFailed: boolean;
  status: StoreProfileDataStatus;
  reload: () => Promise<void>;
};

export const useStoreProfileData = (storeId: string): StoreProfileData => {
  const [profile, setProfile] = useState<StoreInputProfile | null>(null);
  const [profileFailed, setProfileFailed] = useState(false);
  const [status, setStatus] = useState<StoreProfileDataStatus>("loading");

  // One endpoint, one failure mode. The old dual fetch needed allSettled so a failing
  // /unresolved could not blank the profile; there is no second resource left to decouple.
  const load = useCallback(async (): Promise<
    [StoreInputProfile | null, boolean]
  > => {
    try {
      return [await fetchStoreProfile(storeId), false];
    } catch {
      return [null, true];
    }
  }, [storeId]);

  const apply = ([loaded, failed]: [StoreInputProfile | null, boolean]) => {
    setProfile(loaded);
    setProfileFailed(failed);
    setStatus("idle");
  };

  const reload = useCallback(async () => {
    apply(await load());
  }, [load]);

  useEffect(() => {
    let active = true;
    load().then((result) => {
      if (active) apply(result);
    });
    return () => {
      active = false;
    };
  }, [load]);

  return { profile, profileFailed, status, reload };
};
