using Shouldly;
using TeachHub.Domain.Identity;

namespace TeachHub.Domain.Tests;

public class InitialPasswordTests
{
    [Fact]
    public void Is_never_derivable_from_the_student_id()
    {
        // THIS IS WHY THIS CLASS EXISTS. The first version generated the password "Th@{studentId}",
        // and the student id sits right there in the class roster that everyone can read — a student
        // could sign in to a classmate's account BEFORE the first password change, then set a new
        // password of their own. The MustChangePassword flag does not save us: it only blocks
        // AFTER a successful sign-in.
        const string studentId = "2251012001";

        var password = InitialPassword.Generate();

        password.ShouldNotContain(studentId);
        password.ShouldNotBe($"Th@{studentId}");
    }

    [Fact]
    public void Two_calls_produce_two_different_passwords()
    {
        var set = Enumerable.Range(0, 500).Select(_ => InitialPassword.Generate()).ToHashSet();
        set.Count.ShouldBe(500);
    }

    [Fact]
    public void Is_long_enough_to_resist_guessing()
    {
        InitialPassword.Generate().Length.ShouldBeGreaterThanOrEqualTo(12);
    }

    [Fact]
    public void Satisfies_the_Identity_strength_requirements()
    {
        // Identity is configured with RequiredLength = 8 and RequireNonAlphanumeric = false, but it
        // still demands an uppercase letter, a lowercase letter and a digit by default. Generating a
        // password that UserManager itself rejects breaks the "create accounts for the whole class"
        // feature silently.
        for (var i = 0; i < 200; i++)
        {
            var password = InitialPassword.Generate();
            password.ShouldContain(c => char.IsUpper(c));
            password.ShouldContain(c => char.IsLower(c));
            password.ShouldContain(c => char.IsDigit(c));
        }
    }

    [Fact]
    public void Excludes_characters_easily_misheard_when_the_instructor_reads_it_aloud()
    {
        // Same reason as JoinCode: the instructor will read this password out for students to type in.
        var sample = string.Concat(Enumerable.Range(0, 300).Select(_ => InitialPassword.Generate()));

        sample.ShouldNotContain("O");
        sample.ShouldNotContain("l");
        sample.ShouldNotContain("I");
        sample.ShouldNotContain("0");
        sample.ShouldNotContain("1");
    }
}
