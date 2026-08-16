import type { Metadata } from "next";
import { Geist, Geist_Mono } from "next/font/google";
import "./globals.css";
import { Providers } from "./providers";

// ⚠️ "vietnamese" is MANDATORY, not a nice-to-have.
//
// The "latin" subset does NOT contain Vietnamese diacritics (ế, ộ, ữ, ằ, ị, đ…). Without it
// the browser still shows the text — but it renders those particular characters from the
// system's FALLBACK font, so the strokes change weight in the middle of a word ("Bảng điều
// khiển" assembled from two different typefaces). Nothing reports an error, and this is the
// kind of thing that makes a whole product look sloppy while being very hard to point at.
//
// "latin-ext" stays because other European characters still turn up (proper nouns, jargon).
const geistSans = Geist({
  variable: "--font-geist-sans",
  subsets: ["latin", "latin-ext", "vietnamese"],
});

const geistMono = Geist_Mono({
  variable: "--font-geist-mono",
  subsets: ["latin", "latin-ext", "vietnamese"],
});

export const metadata: Metadata = {
  title: "TeachHub",
  description: "Hệ thống quản lý dạy học cá nhân",
};

export default function RootLayout({ children }: LayoutProps<"/">) {
  return (
    // lang="vi" rather than "en": it changes how the browser breaks lines, how spell check
    // behaves, and how a screen reader pronounces Vietnamese content.
    // `suppressHydrationWarning` is REQUIRED by next-themes, not a way of hiding a bug: it
    // sets the theme class from a script that runs BEFORE React takes over the page, so the
    // HTML produced on the server and the HTML in the browser are guaranteed to differ on
    // exactly this tag. Without it every page load logs a hydration warning, and the real
    // warnings drown in that noise. The flag applies only to the <html> tag; it does not
    // cascade into the subtree.
    <html
      lang="vi"
      suppressHydrationWarning
      className={`${geistSans.variable} ${geistMono.variable} h-full antialiased`}
    >
      <body className="min-h-full flex flex-col">
        <Providers>{children}</Providers>
      </body>
    </html>
  );
}
