import { act, renderHook, waitFor } from "@testing-library/react";
import { useStoreProfileData } from "./useStoreProfileData";

vi.mock("./storesApi", () => ({
  fetchStoreProfile: vi.fn(),
}));

import { fetchStoreProfile } from "./storesApi";

const mockFetchProfile = vi.mocked(fetchStoreProfile);

const profile = {
  unresolved: [
    {
      rawLabel: "discord tagg",
      bestCanonical: null,
      bestScore: 70,
      aliasTarget: null,
      itemCount: 1,
      blockedEntryCount: 0,
    },
  ],
  resolved: [
    { labelCanonical: "Email", updatedAt: "2026-01-01", requestedBy: [] },
  ],
  canonicalLabels: ["Email"],
};

beforeEach(() => {
  vi.clearAllMocks();
});

describe("useStoreProfileData", () => {
  it("fetches the profile exactly once", async () => {
    mockFetchProfile.mockResolvedValue(profile);

    const { result } = renderHook(() => useStoreProfileData("store-1"));

    expect(result.current.status).toBe("loading");
    await waitFor(() => expect(result.current.status).toBe("idle"));

    expect(mockFetchProfile).toHaveBeenCalledTimes(1);
    expect(mockFetchProfile).toHaveBeenCalledWith("store-1");
    expect(result.current.profile).toEqual(profile);
    expect(result.current.profileFailed).toBe(false);
  });

  it("reload re-fetches the profile", async () => {
    mockFetchProfile.mockResolvedValue(profile);

    const { result } = renderHook(() => useStoreProfileData("store-1"));
    await waitFor(() => expect(result.current.status).toBe("idle"));

    await act(async () => {
      await result.current.reload();
    });

    expect(mockFetchProfile).toHaveBeenCalledTimes(2);
  });

  // One resource means one failure mode: there is no partial-success state left to model.
  it("reports failure with no partial-success state", async () => {
    mockFetchProfile.mockRejectedValue(new Error("500"));

    const { result } = renderHook(() => useStoreProfileData("store-1"));

    await waitFor(() => expect(result.current.status).toBe("idle"));
    expect(result.current.profile).toBeNull();
    expect(result.current.profileFailed).toBe(true);
  });
});
