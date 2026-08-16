using Shouldly;
using TeachHub.Domain.Integrity;

namespace TeachHub.Domain.Tests;

/// <summary>
/// Source code similarity detection (Phase 6a). The entire value of this algorithm lives in the
/// NORMALIZATION step: without stripping comments and collapsing every identifier to one token,
/// renaming a variable is enough to slip past it, and we ship a feature that manufactures a false
/// sense of safety — strictly worse than having no feature at all, because the instructor will
/// believe the number it reports.
///
/// The whole file runs in under 100 ms, with no I/O.
/// </summary>
public class SourceSimilarityTests
{
    // Two Java submissions doing the SAME thing the SAME way; the second only renames the class,
    // the method and the variables, rewrites the comments and joins some lines. This is exactly
    // what real-world copying looks like.
    private const string JavaOriginal = """
        public class Solution {
            // tinh tong mang
            public static int total(int[] numbers) {
                int sum = 0;
                for (int i = 0; i < numbers.length; i++) {
                    sum += numbers[i];
                }
                return sum;
            }
        }
        """;

    private const string JavaRenamedIdentifiers = """
        public class Bai1 {
            /* cong don cac phan tu cua mang
               (viet lai cho de hieu) */
            public static int cong(int[] a) {
                int s = 0;
                for (int j = 0; j < a.length; j++) { s += a[j]; }
                return s;
            }
        }
        """;

    private const string PythonCompletelyDifferent = """
        def is_prime(n):
            # kiem tra so nguyen to
            if n < 2:
                return False
            for d in range(2, n):
                if n % d == 0:
                    return False
            return True
        """;

    private static List<long> Fingerprints(string source) =>
        [.. Winnowing.Compute(SourceNormalizer.Normalize(source)).Select(f => f.Hash)];

    // -- Normalization --------------------------------------------------------------------

    [Fact]
    public void Renaming_variables_and_methods_does_NOT_change_the_token_stream()
    {
        // This is the single most important check in all of Phase 6a. If it goes red, every
        // similarity number downstream is meaningless.
        SourceNormalizer.Normalize(JavaOriginal)
            .ShouldBe(SourceNormalizer.Normalize(JavaRenamedIdentifiers));
    }

    [Fact]
    public void Keywords_are_preserved_while_identifiers_collapse_to_one_shared_token()
    {
        // Collapsing keywords TOO would make "if (a) b(); else c();" and "int x = y + z;" look
        // alike enough that every submission matches every other — so many warnings that nobody
        // reads them any more.
        var tokens = SourceNormalizer.Normalize("if (soLuong > 0) tong = tong + 1;");

        tokens.ShouldBe(["if", "(", "V", ">", "N", ")", "V", "=", "V", "+", "N", ";"]);
    }

    [Fact]
    public void Single_line_and_multi_line_comments_are_all_stripped()
    {
        SourceNormalizer.Normalize("""
            // chú thích một dòng
            int a; /* chú thích
                      nhiều dòng */ int b;   # chú thích kiểu Python
            """)
            .ShouldBe(["int", "V", ";", "int", "V", ";"]);
    }

    [Fact]
    public void String_literals_collapse_to_one_shared_token()
    {
        // Changing the greeting inside printf is the simplest evasion a student comes up with.
        SourceNormalizer.Normalize("""print("Xin chào");""")
            .ShouldBe(SourceNormalizer.Normalize("""print("Hello world, 2026");"""));
    }

    [Fact]
    public void Python_triple_quoted_strings_do_not_break_the_tokenizer()
    {
        // A naive scan over each double quote reads a triple quote as "empty string + identifier
        // + empty string", and every token after it in the function body is shifted.
        var source = "def f():\n    \"\"\"tài liệu hàm\"\"\"\n    return 1\n";

        SourceNormalizer.Normalize(source)
            .ShouldBe(["def", "V", "(", ")", ":", "S", "return", "N"]);
    }

    [Fact]
    public void Numbers_in_every_format_collapse_to_one_token()
    {
        SourceNormalizer.Normalize("x = 0xFF + 3.14e-2 + 100L;")
            .ShouldBe(["V", "=", "N", "+", "N", "+", "N", ";"]);
    }

    [Fact]
    public void Whitespace_and_line_breaks_make_no_difference()
    {
        SourceNormalizer.Normalize("int a=1;")
            .ShouldBe(SourceNormalizer.Normalize("int   a\n\t=\n1 ;\n"));
    }

    // -- Winnowing ------------------------------------------------------------------------

    [Fact]
    public void The_token_hash_does_NOT_use_the_runtime_GetHashCode()
    {
        // string.GetHashCode() is randomized per process. Using it means a fingerprint stored
        // today does not match the same fingerprint recomputed after the next restart — a table
        // full of data where every comparison comes out at 0%, and no error to trace.
        // This test recomputes FNV-1a 64-bit INDEPENDENTLY and compares, rather than calling
        // back into the very function under test.
        const string token = "for";
        var expected = 0xCBF29CE484222325UL;
        foreach (var c in token)
        {
            expected ^= c;
            expected = unchecked(expected * 0x100000001B3UL);
        }

        Winnowing.HashToken(token).ShouldBe(unchecked((long)expected));
    }

    [Fact]
    public void Hashing_the_same_input_twice_gives_the_same_result()
    {
        Fingerprints(JavaOriginal).ShouldBe(Fingerprints(JavaOriginal));
    }

    [Fact]
    public void The_rolling_hash_agrees_with_computing_each_k_gram_directly()
    {
        var tokens = SourceNormalizer.Normalize(JavaOriginal);

        var rolling = Winnowing.KGramHashes(tokens, 5);
        var direct = Enumerable.Range(0, tokens.Count - 5 + 1)
            .Select(i => Winnowing.HashKGram(tokens.Skip(i).Take(5).ToList()))
            .ToList();

        rolling.ShouldBe(direct);
    }

    [Fact]
    public void An_empty_file_produces_no_fingerprints_and_does_not_blow_up()
    {
        Winnowing.Compute([]).ShouldBeEmpty();
        Winnowing.Compute(SourceNormalizer.Normalize("")).ShouldBeEmpty();
        Winnowing.Compute(SourceNormalizer.Normalize("   \n\n  // chỉ có chú thích\n")).ShouldBeEmpty();
    }

    [Fact]
    public void A_file_shorter_than_one_k_gram_produces_no_fingerprints()
    {
        // 3 tokens with k = 5: there is no k-gram at all. Return empty rather than throwing.
        Winnowing.Compute(["int", "V", ";"]).ShouldBeEmpty();
    }

    [Fact]
    public void A_file_smaller_than_one_window_still_yields_a_fingerprint()
    {
        // 6 tokens, k = 5 => 2 k-grams, fewer than the window w = 4. Skipping them entirely would
        // make every short function invisible to the system, and copying a short function is the
        // easiest evasion left.
        Winnowing.Compute(["int", "V", "=", "N", ";", "}"]).ShouldNotBeEmpty();
    }

    [Fact]
    public void The_returned_position_points_at_the_k_gram_that_was_selected()
    {
        var tokens = SourceNormalizer.Normalize(JavaOriginal);
        var hashes = Winnowing.KGramHashes(tokens, 5);

        foreach (var f in Winnowing.Compute(tokens))
        {
            f.Position.ShouldBeInRange(0, hashes.Count - 1);
            hashes[f.Position].ShouldBe(f.Hash);
        }
    }

    [Fact]
    public void Winnowing_selects_substantially_fewer_entries_than_there_are_k_grams()
    {
        // Without an actual reduction, the source_fingerprints table grows to the total token
        // count of the entire class, and Gap #14 (an index on the hash) will not save it.
        var tokens = SourceNormalizer.Normalize(JavaOriginal);

        Winnowing.Compute(tokens).Count
            .ShouldBeLessThan(Winnowing.KGramHashes(tokens, 5).Count);
    }

    // -- Similarity scoring ---------------------------------------------------------------

    [Fact]
    public void Two_submissions_differing_only_in_identifier_names_score_very_high()
    {
        var result = SimilarityScorer.Compare(Fingerprints(JavaOriginal), Fingerprints(JavaRenamedIdentifiers));

        result.ContainmentPercent.ShouldBe(100m);
        result.JaccardPercent.ShouldBe(100m);
    }

    [Fact]
    public void Two_completely_different_submissions_score_low()
    {
        var result = SimilarityScorer.Compare(Fingerprints(JavaOriginal), Fingerprints(PythonCompletelyDifferent));

        result.ContainmentPercent.ShouldBeLessThan(20m);
    }

    [Fact]
    public void Comparing_two_empty_sets_does_not_divide_by_0()
    {
        var result = SimilarityScorer.Compare([], []);

        result.ContainmentPercent.ShouldBe(0m);
        result.JaccardPercent.ShouldBe(0m);
        result.SharedHashes.ShouldBe(0);
    }

    [Fact]
    public void With_one_side_empty_nothing_matches()
    {
        SimilarityScorer.Compare(Fingerprints(JavaOriginal), []).ContainmentPercent.ShouldBe(0m);
        SimilarityScorer.Compare([], Fingerprints(JavaOriginal)).ContainmentPercent.ShouldBe(0m);
    }

    [Fact]
    public void Pasting_a_fragment_into_a_much_longer_submission_is_still_caught_by_Containment()
    {
        // Jaccard divides by the UNION of the two sets, so it dilutes when the copying submission
        // is far longer than the original. Containment divides by the SMALLER set, so "copy a
        // whole function then write 300 lines of my own" still shows up — which is why the system
        // reports both numbers.
        var longer = JavaRenamedIdentifiers + "\n" + PythonCompletelyDifferent + "\n" + PythonCompletelyDifferent;

        var result = SimilarityScorer.Compare(Fingerprints(JavaOriginal), Fingerprints(longer));

        result.ContainmentPercent.ShouldBe(100m);
        result.JaccardPercent.ShouldBeLessThan(result.ContainmentPercent);
    }

    [Fact]
    public void Matched_positions_are_what_highlight_the_identical_passages()
    {
        var a = Winnowing.Compute(SourceNormalizer.Normalize(JavaOriginal));
        var b = Fingerprints(JavaRenamedIdentifiers).ToHashSet();

        SimilarityScorer.MatchedPositions(a, b).Count.ShouldBe(a.Count);
        SimilarityScorer.MatchedPositions(a, []).ShouldBeEmpty();
    }

    // -- Warning threshold ----------------------------------------------------------------

    [Fact]
    public void The_warning_threshold_is_a_parameter_not_an_arbitrarily_chosen_constant()
    {
        // Introductory assignments sharing starter code legitimately match at 70-90%.
        // A hardcoded threshold of 70 would flag the entire class in the first week.
        SimilarityThresholds.ShouldWarn(85m, 70m).ShouldBeTrue();
        SimilarityThresholds.ShouldWarn(85m, 95m).ShouldBeFalse();
        SimilarityThresholds.ShouldWarn(95m, 95m).ShouldBeTrue();
    }

    [Fact]
    public void Source_files_are_recognized_so_that_images_and_pdfs_are_not_hashed()
    {
        SourceFileTypes.IsSourceCode("Main.java").ShouldBeTrue();
        SourceFileTypes.IsSourceCode("src/utils/helper.PY").ShouldBeTrue();
        SourceFileTypes.IsSourceCode("bao-cao.pdf").ShouldBeFalse();
        SourceFileTypes.IsSourceCode("anh.png").ShouldBeFalse();
        SourceFileTypes.IsSourceCode("khong-co-duoi").ShouldBeFalse();
    }
}
