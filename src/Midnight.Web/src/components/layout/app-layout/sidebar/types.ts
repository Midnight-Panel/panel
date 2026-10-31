import React from "react";

export type MenuType = "main" | "admin";

export interface SubItem {
  /** Translation key */
  key: string;
  path: string;
  permission?: string;
}

export interface NavItem {
  /** Translation key */
  key: string;
  icon: React.ReactNode;
  path?: string;
  /** Only shown with this permission */
  permission?: string;
  /** Path prefixes that also mark this item as active */
  matchPrefix?: string;
  subItems?: SubItem[];
}
