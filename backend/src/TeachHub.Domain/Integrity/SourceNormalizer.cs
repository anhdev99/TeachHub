namespace TeachHub.Domain.Integrity;

/// <summary>
/// A normalized token together with its position in the ORIGINAL text. Keeping the original
/// position is mandatory for the side-by-side comparison screen: after normalization every
/// variable name is just "V", so without remembering which characters this token came from there
/// is no way to highlight the matching stretch back on the real source code.
/// </summary>
public readonly record struct SourceToken(string Text, int Offset, int Length);

/// <summary>
/// Normalizes source code into a token sequence before hashing. THIS IS THE ENTIRE VALUE of the
/// plagiarism-detection mechanism.
///
/// Without normalization, renaming variables, editing comments and hitting auto-format is enough
/// to get past it, and we end up with a feature that creates a false sense of safety — strictly
/// worse than having no feature at all, because the instructor will trust the number it reports
/// and stop reading submissions with their own eyes.
///
/// Four normalization steps, in order of importance:
/// 1. Strip comments (single-line, multi-line, and the <c>#</c> kind).
/// 2. Strip whitespace and line breaks.
/// 3. Fold every identifier into ONE shared token <c>V</c>, but KEEP KEYWORDS AS THEY ARE.
/// 4. Fold every string literal into <c>S</c> and every number into <c>N</c>.
///
/// Why keywords must be kept: fold keywords into "V" as well and <c>if (a) b(); else c();</c> and
/// <c>int x = y + z;</c> look alike to the point where every submission matches every other one —
/// the warning list grows so long that nobody reads it any more, which loses the feature by a
/// different route.
///
/// Pure function, no I/O, not tied to any one programming language. The rule set deliberately
/// covers the C family (C/C++/C#/Java/JS/TS) and Python together rather than building a separate
/// parser per language: splitting tokens this way is already enough to spot a copied submission,
/// while a dedicated parser is a maintenance cost multiplied by the number of languages students
/// submit in.
/// </summary>
public static class SourceNormalizer
{
    /// <summary>Every variable name, function name and class name becomes this token.</summary>
    public const string IdentifierToken = "V";

    /// <summary>Every constant — integer, floating point, hex — becomes this token.</summary>
    public const string NumberToken = "N";

    /// <summary>Every string literal becomes this token: changing the greeting in a printf is no escape.</summary>
    public const string StringToken = "S";

    /// <summary>
    /// Keywords of the C family and of Python merged into one set. Mistaking an identifier for a
    /// keyword (a variable named <c>string</c>, say) only makes that one token slightly more
    /// specific, it does not break the result — because both submissions are normalized with this
    /// very same rule set.
    /// </summary>
    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "if", "else", "elif", "for", "while", "do", "switch", "case", "default", "break",
        "continue", "return", "goto", "foreach", "in", "of", "is", "as", "with", "yield",
        "class", "struct", "interface", "enum", "record", "namespace", "package", "module",
        "import", "using", "from", "export", "extends", "implements", "abstract", "override",
        "virtual", "sealed", "partial", "final", "static", "const", "readonly", "volatile",
        "public", "private", "protected", "internal", "friend", "extern", "inline", "register",
        "new", "delete", "this", "self", "super", "base", "null", "none", "nil", "nullptr",
        "true", "false", "and", "or", "not", "try", "catch", "except", "finally", "throw",
        "throws", "raise", "assert", "pass", "global", "nonlocal", "lambda", "def", "function",
        "func", "fn", "var", "let", "async", "await", "void", "int", "long", "short", "byte",
        "float", "double", "char", "bool", "boolean", "string", "str", "unsigned", "signed",
        "template", "typename", "typedef", "operator", "sizeof", "typeof", "instanceof",
        "delegate", "event", "out", "ref", "params",
    };

    /// <summary>The normalized token sequence — this is the input to <see cref="Winnowing"/>.</summary>
    public static IReadOnlyList<string> Normalize(string? source) =>
        [.. Tokenize(source).Select(t => t.Text)];

    public static IReadOnlyList<SourceToken> Tokenize(string? source)
    {
        var result = new List<SourceToken>();
        if (string.IsNullOrEmpty(source)) return result;

        var n = source.Length;
        var i = 0;

        while (i < n)
        {
            var c = source[i];

            if (char.IsWhiteSpace(c)) { i++; continue; }

            // ── Comments ─────────────────────────────────────────────────────────────────
            // '#' swallows the whole line, so C preprocessor directives (#include, #define)
            // disappear too. Deliberate: that is boilerplate identical across every submission,
            // and keeping it only makes every submission match every other one further without
            // carrying any information about who copied from whom.
            if (c == '#' || (c == '/' && i + 1 < n && source[i + 1] == '/'))
            {
                while (i < n && source[i] != '\n') i++;
                continue;
            }

            if (c == '/' && i + 1 < n && source[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < n && !(source[i] == '*' && source[i + 1] == '/')) i++;
                i = Math.Min(n, i + 2);          // unclosed comment ⇒ swallow to end of file
                continue;
            }

            // ── String literals ──────────────────────────────────────────────────────────
            if (c is '"' or '\'')
            {
                var start = i;
                i = SkipString(source, i);
                result.Add(new SourceToken(StringToken, start, i - start));
                continue;
            }

            // ── Numbers ──────────────────────────────────────────────────────────────────
            if (char.IsAsciiDigit(c))
            {
                var start = i;
                i = SkipNumber(source, i);
                result.Add(new SourceToken(NumberToken, start, i - start));
                continue;
            }

            // ── Identifiers / keywords ───────────────────────────────────────────────────
            if (c == '_' || char.IsLetter(c))
            {
                var start = i;
                while (i < n && (source[i] == '_' || char.IsLetterOrDigit(source[i]))) i++;

                var word = source[start..i].ToLowerInvariant();
                result.Add(new SourceToken(
                    Keywords.Contains(word) ? word : IdentifierToken, start, i - start));
                continue;
            }

            // ── Punctuation and operators ────────────────────────────────────────────────
            // ONE TOKEN PER CHARACTER, no merging of "==" or "+=" into a single token. Merging
            // or not merging is equally correct as long as both submissions follow the same
            // rule; splitting per character means there is no operator table to maintain for
            // each new language.
            result.Add(new SourceToken(source[i].ToString(), i, 1));
            i++;
        }

        return result;
    }

    /// <summary>Returns the index just AFTER the string literal starting at <paramref name="i"/>.</summary>
    private static int SkipString(string s, int i)
    {
        var n = s.Length;
        var quote = s[i];

        // Triple quotes (Python docstring, C# raw string). A naive scan quote by quote would read
        // """abc""" as empty string + identifier abc + empty string, and EVERY token after that
        // point in the file shifts — two identical submissions would come out at 0% similar.
        if (i + 2 < n && s[i + 1] == quote && s[i + 2] == quote)
        {
            i += 3;
            while (i + 2 < n && !(s[i] == quote && s[i + 1] == quote && s[i + 2] == quote)) i++;
            return i + 2 < n ? i + 3 : n;
        }

        i++;
        while (i < n)
        {
            if (s[i] == '\\') { i += 2; continue; }        // escape sequence: '\'' , "\""
            if (s[i] == quote) return i + 1;

            // String left unclosed before the end of the line: stop here instead of swallowing
            // the whole rest of the file. Usually a stray apostrophe inside an unaccented
            // Vietnamese comment ("don't").
            if (s[i] == '\n') return i;

            i++;
        }

        return n;
    }

    /// <summary>Returns the index just AFTER the numeric constant starting at <paramref name="i"/>.</summary>
    private static int SkipNumber(string s, int i)
    {
        var n = s.Length;

        while (i < n)
        {
            var c = s[i];

            // Signed exponent: 3.14e-2, 1E+9. Without handling it specially, "e" would end the
            // number and "-2" would split off as a minus operator — a token sequence that no
            // longer lines up with the same number written a different way.
            if ((c is 'e' or 'E') && i + 2 < n && s[i + 1] is '+' or '-' && char.IsAsciiDigit(s[i + 2]))
            {
                i += 2;
                continue;
            }

            // Letters are accepted so that 0xFF, 100L, 1_000 and 3.14f are swallowed whole.
            if (char.IsLetterOrDigit(c) || c is '.' or '_') { i++; continue; }

            break;
        }

        return i;
    }
}

/// <summary>
/// Which files are worth hashing at all. Hashing images and PDFs too would bloat the fingerprint
/// table with meaningless binary data, and two submissions containing the same assignment-brief
/// image would be scored as 100% identical.
/// </summary>
public static class SourceFileTypes
{
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".c", ".h", ".cpp", ".cc", ".cxx", ".hpp", ".hh", ".cs", ".java", ".kt", ".kts",
        ".py", ".js", ".jsx", ".mjs", ".cjs", ".ts", ".tsx", ".go", ".rs", ".php", ".rb",
        ".swift", ".scala", ".m", ".mm", ".sql", ".r", ".pas", ".dart", ".lua", ".pl", ".sh",
    };

    public static bool IsSourceCode(string? fileName) =>
        !string.IsNullOrWhiteSpace(fileName)
        && Path.GetExtension(fileName) is { Length: > 0 } ext
        && Extensions.Contains(ext);
}
