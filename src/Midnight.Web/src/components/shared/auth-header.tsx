export function AuthHeader({ title, description }: { title: string; description: string }) {
  return (
    <div className="mb-5 sm:mb-8">
      <h1 className="sm:text-title-md mb-2 text-title-sm font-semibold text-gray-800 dark:text-white/90">{title}</h1>
      <p className="text-sm text-gray-500 dark:text-gray-400">{description}</p>
    </div>
  );
}
