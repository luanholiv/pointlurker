import { setupServer } from "msw/node";
import { authHandlers } from "./handlers/auth";
import { storesHandlers } from "./handlers/stores";
import { wishlistHandlers } from "./handlers/wishlist";
import { redemptionHandlers } from "./handlers/redemptions";

export const server = setupServer(
  ...authHandlers,
  ...storesHandlers,
  ...wishlistHandlers,
  ...redemptionHandlers,
);
