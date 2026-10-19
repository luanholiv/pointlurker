export type AppEnv = {
  VITE_API_URL?: string;
};

export const resolveApiBaseUrl = (env: AppEnv = import.meta.env): string => {
  const value = env.VITE_API_URL?.trim();

  if (!value) {
    if (typeof window !== "undefined" && window.location?.origin) {
      return window.location.origin;
    }

    throw new Error("VITE_API_URL is required to run the frontend");
  }

  return value;
};

export const getAppConfig = (env: AppEnv = import.meta.env) => ({
  apiBaseUrl: resolveApiBaseUrl(env),
});
