import { redirect } from "next/navigation";
import { StudentTopbar } from "@/components/shell/student-topbar";
import { getCurrentUser, initialOf } from "@/lib/api/me";

/**
 * The shared frame for the student role. Deliberately minimal: SRS §9 says students mostly
 * view this on a phone, so every vertical pixel spent on navigation is a pixel taken from the
 * content. No sidebar — a student has only a handful of destinations, and all of them start
 * from the class page.
 *
 * Do NOT gate by role here. An instructor opening a lesson link for their own class is
 * legitimate and common (checking what students actually see); the API still enforces
 * permissions on every endpoint, so blocking here only creates a dead end for no reason.
 */
export default async function StudentLayout({ children }: LayoutProps<"/">) {
  const me = await getCurrentUser();
  if (!me) redirect("/login");

  return (
    <div className="flex min-h-svh flex-col">
      <StudentTopbar name={me.full_name ?? "Sinh viên"} initial={initialOf(me.full_name)} />
      {children}
    </div>
  );
}
