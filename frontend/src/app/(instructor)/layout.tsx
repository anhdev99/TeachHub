import { redirect } from "next/navigation";
import { InstructorNav } from "@/components/shell/instructor-nav";
import { getCurrentUser, initialOf } from "@/lib/api/me";

/**
 * The shared frame for every instructor screen.
 *
 * Before this file existed, each page was a bare `<main>`: from the dashboard there was NO
 * route to Subjects or to the Question Bank — you had to type the URL. That was not a
 * cosmetic problem but a missing layer: the design says each role's route group must have its
 * own layout, and it had never been built.
 *
 * Because it sits in the route group's layout, **every page added later gets navigation for
 * free**, with nothing to remember. If each page drew its own sidebar, the tenth page would
 * forget — and that would be the page that looks broken.
 */
export default async function InstructorLayout({ children }: LayoutProps<"/">) {
  const me = await getCurrentUser();
  if (!me) redirect("/login");

  // A student who wanders into the instructor branch goes to the learning page instead of
  // being shown a full navigation frame where every item 404s.
  if (!me.roles?.includes("Instructor")) redirect("/learn");

  return (
    <div className="lg:pl-[17rem]">
      <InstructorNav
        name={me.full_name ?? "Giảng viên"}
        email={me.email ?? ""}
        initial={initialOf(me.full_name)}
      />
      {children}
    </div>
  );
}
