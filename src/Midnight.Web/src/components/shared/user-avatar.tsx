import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar";
import type { ComponentProps } from "react";

/** User avatar with uploaded image or initials on the brand gradient */
export function UserAvatar({ userId, hasAvatar, name, version = 0, size, className }: {
  userId: number;
  hasAvatar: boolean;
  name: string;
  version?: number;
  size?: ComponentProps<typeof Avatar>["size"];
  className?: string;
}) {
  return (
    <Avatar size={size} className={className}>
      {hasAvatar && <AvatarImage src={`/api/users/${userId}/avatar?v=${version}`} alt={name} />}
      <AvatarFallback>
        {name.slice(0, 2).toUpperCase()}
      </AvatarFallback>
    </Avatar>
  );
}
