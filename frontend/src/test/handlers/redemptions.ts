import { http, HttpResponse } from "msw";

const BASE = "http://localhost";

export const redemptionHandlers = [
  // The shared page envelope: slice bounds are an offset and a limit, as on every other list.
  http.get(`${BASE}/api/redemptions`, () =>
    HttpResponse.json({
      items: [],
      totalCount: 0,
      offset: 0,
      limit: 20,
    }),
  ),
];
