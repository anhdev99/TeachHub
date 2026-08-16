"use client";

import { useEffect, useState } from "react";

/**
 * One span to highlight inside the source, measured in CHARACTER OFFSETS on the original text.
 *
 * Matches `MatchedRangeDto` from the submission comparison API one-to-one. There is no point
 * re-declaring that type here — callers pass `MatchedRangeDto[]` straight in and TypeScript
 * accepts it, because the `position`/`length` fields match structurally. It is declared
 * separately so this component does not have to drag in the whole API schema for two numbers.
 */
export type HighlightRange = { position: number; length: number };

/**
 * Infer the language from the file extension. Anything unrecognized falls back to "text" —
 * Shiki throws on an unknown language, and one .cfg file inside a submission must not be able
 * to break the entire grading page.
 */
function languageOf(path: string): string {
  const ext = path.split(".").pop()?.toLowerCase() ?? "";
  const byExtension: Record<string, string> = {
    ts: "typescript", tsx: "tsx", js: "javascript", jsx: "jsx",
    json: "json", css: "css", scss: "scss", html: "html",
    md: "markdown", py: "python", java: "java", cs: "csharp",
    sql: "sql", sh: "bash", yml: "yaml", yaml: "yaml", xml: "xml",
    kt: "kotlin", swift: "swift", go: "go", rs: "rust", php: "php", c: "c", cpp: "cpp",
  };
  return byExtension[ext] ?? "text";
}

/**
 * Merge overlapping ranges and clamp them into [0, length].
 *
 * Merging is MANDATORY: Winnowing fingerprints are SLIDING k-grams, so two adjacent ranges
 * almost always overlap. Shiki THROWS when two decorations intersect, so skipping this step
 * makes the whole code block disappear and be replaced by an error message — at exactly the
 * moment the instructor most needs to read it.
 *
 * Clamping is not redundant either: the offsets were computed while hashing the submission,
 * while the content being displayed is what gets read back out of storage right now. A
 * one-byte drift (encoding, trailing newline) without clamping also makes Shiki throw,
 * because the decoration runs past the end of the string.
 */
function mergeRanges(ranges: readonly HighlightRange[], length: number) {
  const spans = ranges
    .map((r) => ({ start: Math.max(0, r.position), end: Math.min(length, r.position + r.length) }))
    .filter((s) => s.end > s.start)
    .sort((a, b) => a.start - b.start);

  const merged: { start: number; end: number }[] = [];
  for (const span of spans) {
    const last = merged.at(-1);
    if (last && span.start <= last.end) last.end = Math.max(last.end, span.end);
    else merged.push({ ...span });
  }
  return merged;
}

/**
 * View one file inside a submission, with syntax highlighting and (optionally) highlighted
 * ranges that match another submission.
 *
 * Shiki takes PLAIN TEXT and emits already-escaped HTML — it never executes its input. This
 * is the ONLY place in this project where dangerouslySetInnerHTML is allowed: every other way
 * of rendering student-submitted content is an XSS hole running inside the instructor's
 * session — that is, the session that can change the whole class's grades.
 *
 * Dynamically imported so the (heavy) highlighter stays out of the submission page's bundle,
 * where most students never open a file to look at it.
 */
export function SourceViewer({
  submissionId,
  fileId,
  path,
  fileName,
  highlights,
}: {
  submissionId: string;
  fileId: string;
  /** Path inside the archive. Empty when the student submitted a bare source file. */
  path?: string | null;
  /** File name, used only to guess the language when there is no `path`. */
  fileName?: string;
  highlights?: readonly HighlightRange[];
}) {
  const [html, setHtml] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  // The array is a fresh reference after every render of the parent component, so do NOT put
  // `highlights` directly into the deps: the effect would re-run forever and reload the file
  // without stopping. Comparing the serialized content is what actually determines the
  // rendered result.
  const highlightKey = JSON.stringify(highlights ?? []);

  useEffect(() => {
    let cancelled = false;

    (async () => {
      setHtml(null);
      setError(null);

      try {
        const q = path ? `?path=${encodeURIComponent(path)}` : "";
        const res = await fetch(`/api/submissions/${submissionId}/files/${fileId}/entry${q}`);

        if (!res.ok) {
          // 404 = not found OR not yours (the backend deliberately never returns 403).
          // That is why we never write "bạn không có quyền" here.
          const problem = await res.json().catch(() => null);
          throw new Error(problem?.detail ?? `Lỗi ${res.status}`);
        }

        // The backend returns text/plain + Content-Disposition: attachment. fetch reads the
        // body normally; that header only stops the browser from rendering the file in a tab.
        const text = await res.text();
        const { codeToHtml } = await import("shiki");

        const spans = mergeRanges(
          (JSON.parse(highlightKey) as HighlightRange[]),
          text.length,
        );

        // defaultColor: "light" writes the light colors DIRECTLY into the inline style, while
        // the dark colors ride along as --shiki-dark variables. Setting `false` for both
        // applies neither, and the code block renders in black and white — exactly what we
        // saw in testing. The rule that reads the dark variables lives in globals.css.
        const out = await codeToHtml(text, {
          lang: languageOf(path ?? fileName ?? ""),
          themes: { light: "github-light", dark: "github-dark" },
          defaultColor: "light",
          // Shiki splits decorations per line and escapes them itself — a highlighted range
          // is still just text, and opens no additional path for student content to reach the
          // DOM as HTML.
          decorations: spans.map((s) => ({
            start: s.start,
            end: s.end,
            properties: { class: "th-match" },
          })),
        });

        if (!cancelled) setHtml(out);
      } catch (e) {
        if (!cancelled) setError(e instanceof Error ? e.message : "Không đọc được tệp.");
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [submissionId, fileId, path, fileName, highlightKey]);

  // The wording comes verbatim from the backend's `ProblemDetails.detail` — it was already
  // written in Vietnamese for a human reader. Wrapped in a `danger` box so it is
  // distinguishable from the file's own content.
  if (error)
    return (
      <p className="bg-danger-soft text-danger border-danger/25 rounded-lg border px-3 py-2 text-sm text-pretty">
        {error}
      </p>
    );

  if (html === null)
    return (
      <p className="border-border text-muted-foreground rounded-lg border border-dashed px-3 py-2 text-sm">
        Đang tải tệp…
      </p>
    );

  return (
    <div
      className="[&_pre]:overflow-x-auto [&_pre]:rounded-lg [&_pre]:border [&_pre]:p-3 [&_pre]:text-xs"
      // eslint-disable-next-line react/no-danger -- Shiki emits escaped HTML from raw text
      dangerouslySetInnerHTML={{ __html: html }}
    />
  );
}
