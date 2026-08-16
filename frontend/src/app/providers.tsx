"use client";

import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { ThemeProvider } from "next-themes";
import { useState } from "react";

export function Providers({ children }: { children: React.ReactNode }) {
  // Create the QueryClient in state rather than at module scope: on the server, module scope
  // is shared between requests, and one instructor's data would leak into another's.
  const [client] = useState(
    () =>
      new QueryClient({
        defaultOptions: {
          queries: { staleTime: 30_000, retry: 1, refetchOnWindowFocus: false },
        },
      }),
  );

  return (
    // `attribute="class"` has to match `@custom-variant dark (&:is(.dark *))` in globals.css
    // — next-themes writes `data-theme` by default, while this project's `dark` variant reads
    // a class, so leaving the default means the dark palette is fully defined but never
    // activated.
    //
    // `defaultTheme="system"` follows the OS setting: an instructor grading late at night
    // should not have to go hunting for a switch, and anyone who wants to differ from the
    // system can still change it.
    <ThemeProvider
      attribute="class"
      defaultTheme="system"
      enableSystem
      disableTransitionOnChange
    >
      <QueryClientProvider client={client}>{children}</QueryClientProvider>
    </ThemeProvider>
  );
}
