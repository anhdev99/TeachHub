using Shouldly;
using TeachHub.Domain.Teaching;

namespace TeachHub.Domain.Tests;

public class JoinCodeTests
{
    [Fact]
    public void Code_is_6_uppercase_letters_or_digits()
    {
        JoinCode.Generate().Length.ShouldBe(6);
        JoinCode.Generate().ShouldAllBe(c => char.IsUpper(c) || char.IsDigit(c));
    }

    [Fact]
    public void Code_excludes_characters_easily_misheard_when_read_aloud_in_class()
    {
        // The instructor reads this code out loud for the whole class to type in. 0/O and 1/I/L
        // cause enough mistyping to ruin an entire first session.
        var sample = string.Concat(Enumerable.Range(0, 500).Select(_ => JoinCode.Generate()));

        sample.ShouldNotContain("0");
        sample.ShouldNotContain("O");
        sample.ShouldNotContain("1");
        sample.ShouldNotContain("I");
        sample.ShouldNotContain("L");
    }

    [Fact]
    public void Generated_codes_are_sufficiently_spread_out()
    {
        var set = Enumerable.Range(0, 1000).Select(_ => JoinCode.Generate()).ToHashSet();
        set.Count.ShouldBeGreaterThan(995);   // allow a very small number of random collisions
    }
}
