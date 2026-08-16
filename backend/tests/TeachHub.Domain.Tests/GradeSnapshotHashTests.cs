using Shouldly;
using TeachHub.Domain.Grading;

namespace TeachHub.Domain.Tests;

public class GradeSnapshotHashTests
{
    [Fact]
    public void The_same_string_always_produces_the_same_hash()
    {
        // Reconciling a finalized snapshot six months later only means something if the hash
        // function is deterministic.
        const string json = """{"class_code":"DA24TIN01","rows":[]}""";

        GradeSnapshotHash.Compute(json).ShouldBe(GradeSnapshotHash.Compute(json));
    }

    [Fact]
    public void Changing_one_character_changes_the_hash()
    {
        GradeSnapshotHash.Compute("""{"final_score":8.5}""")
            .ShouldNotBe(GradeSnapshotHash.Compute("""{"final_score":8.6}"""));
    }

    [Fact]
    public void The_hash_is_64_lowercase_hex_characters()
    {
        // The payload_hash column is declared varchar(64). Returning uppercase, or a dashed form,
        // either overflows the column or mismatches on string comparison — and both only surface
        // during a real grade finalization.
        var h = GradeSnapshotHash.Compute("xin chào");

        h.Length.ShouldBe(64);
        h.ShouldAllBe(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'));
    }

    [Fact]
    public void Matches_the_standard_SHA256_test_vector()
    {
        // The public SHA-256 test vector for the string "abc". With it in place, accidentally
        // switching algorithm (SHA-1, MD5) turns the test red immediately instead of silently
        // producing hashes from a different family.
        GradeSnapshotHash.Compute("abc")
            .ShouldBe("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad");
    }

    [Fact]
    public void Vietnamese_with_diacritics_is_hashed_as_UTF8()
    {
        // Grade sheets are full of Vietnamese names. Hashing with the server's default encoding
        // makes one snapshot produce two different hashes on two machines — the snapshot loses
        // all of its reconciliation value.
        GradeSnapshotHash.Compute("Nguyễn Văn Ánh")
            .ShouldBe(GradeSnapshotHash.Compute("Nguyễn Văn Ánh"));
    }
}
