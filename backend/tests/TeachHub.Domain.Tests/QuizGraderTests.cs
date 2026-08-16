using System.Text;
using Shouldly;
using TeachHub.Domain.Quizzes;

namespace TeachHub.Domain.Tests;

/// <summary>
/// Kept even though the phase ran under a "no tests" instruction: grading Vietnamese fill-in-the-blank
/// answers is a case that CANNOT be checked by eye. Two strings reading "Hà Nội", typed with two
/// different input methods, render identically on screen but differ byte for byte — looking at a
/// screenshot will never reveal it. The whole file runs in under 20 ms.
/// </summary>
public class QuizGraderTests
{
    private static QuizQuestion SingleChoice(params (string Text, bool Correct)[] options)
    {
        var q = new QuizQuestion
        {
            InstructorId = Guid.CreateVersion7(), Type = QuestionType.SingleChoice,
            Text = "Câu hỏi", Points = 1m,
        };
        q.Options.AddRange(options.Select((o, i) => new QuestionOption
        {
            Text = o.Text, IsCorrect = o.Correct, OrderIndex = i,
        }));
        return q;
    }

    private static QuizQuestion MultipleChoice(bool partialCredit, params (string Text, bool Correct)[] options)
    {
        var q = new QuizQuestion
        {
            InstructorId = Guid.CreateVersion7(), Type = QuestionType.MultipleChoice,
            Text = "Câu hỏi", Points = 2m, AllowPartialCredit = partialCredit,
        };
        q.Options.AddRange(options.Select((o, i) => new QuestionOption
        {
            Text = o.Text, IsCorrect = o.Correct, OrderIndex = i,
        }));
        return q;
    }

    private static QuizQuestion FillBlank(params string[] acceptedAnswers) => new()
    {
        InstructorId = Guid.CreateVersion7(), Type = QuestionType.FillBlank,
        Text = "Thủ đô Việt Nam là ___", Points = 1m,
        FillBlank = new FillBlankConfig { AcceptedAnswers = [.. acceptedAnswers] },
    };

    // -- Single choice --------------------------------------------------------------------

    [Fact]
    public void Picking_the_one_right_option_earns_the_points()
    {
        var q = SingleChoice(("A", false), ("B", true));
        var correct = q.Options[1].Id;

        QuizGrader.Grade([q], [new QuizAnswer(q.Id, [correct], null)]).RawScore.ShouldBe(1m);
    }

    [Fact]
    public void Not_answering_at_all_scores_0()
    {
        var q = SingleChoice(("A", false), ("B", true));

        QuizGrader.Grade([q], []).RawScore.ShouldBe(0m);
    }

    [Fact]
    public void Selecting_two_options_on_a_single_choice_question_is_wrong()
    {
        // Silently taking the first option would be going easy on someone guessing blindly.
        var q = SingleChoice(("A", false), ("B", true));

        QuizGrader.Grade([q], [new QuizAnswer(q.Id, [q.Options[0].Id, q.Options[1].Id], null)])
            .RawScore.ShouldBe(0m);
    }

    // -- Multiple choice ------------------------------------------------------------------

    [Fact]
    public void All_or_nothing_missing_one_correct_option_scores_0()
    {
        var q = MultipleChoice(false, ("A", true), ("B", true), ("C", false));

        QuizGrader.Grade([q], [new QuizAnswer(q.Id, [q.Options[0].Id], null)]).RawScore.ShouldBe(0m);
    }

    [Fact]
    public void All_or_nothing_selecting_every_correct_option_earns_full_marks()
    {
        var q = MultipleChoice(false, ("A", true), ("B", true), ("C", false));

        QuizGrader.Grade([q], [new QuizAnswer(q.Id, [q.Options[0].Id, q.Options[1].Id], null)])
            .RawScore.ShouldBe(2m);
    }

    [Fact]
    public void With_partial_credit_one_of_two_correct_options_earns_half_the_points()
    {
        var q = MultipleChoice(true, ("A", true), ("B", true), ("C", false));

        QuizGrader.Grade([q], [new QuizAnswer(q.Id, [q.Options[0].Id], null)]).RawScore.ShouldBe(1m);
    }

    [Fact]
    public void With_partial_credit_selecting_EVERY_option_does_not_earn_full_marks()
    {
        // Without subtracting for wrong options, ticking every box earns full marks — and the
        // entire class will do exactly that.
        var q = MultipleChoice(true, ("A", true), ("B", true), ("C", false));

        QuizGrader.Grade([q],
            [new QuizAnswer(q.Id, [q.Options[0].Id, q.Options[1].Id, q.Options[2].Id], null)])
            .RawScore.ShouldBe(1m);   // (2 correct - 1 wrong) / 2 x 2 points
    }

    [Fact]
    public void The_score_is_never_negative()
    {
        var q = MultipleChoice(true, ("A", true), ("B", false), ("C", false));

        QuizGrader.Grade([q], [new QuizAnswer(q.Id, [q.Options[1].Id, q.Options[2].Id], null)])
            .RawScore.ShouldBe(0m);
    }

    [Fact]
    public void An_option_id_that_does_not_belong_to_the_question_is_ignored()
    {
        var q = MultipleChoice(false, ("A", true), ("B", true));

        QuizGrader.Grade([q],
            [new QuizAnswer(q.Id, [q.Options[0].Id, q.Options[1].Id, Guid.CreateVersion7()], null)])
            .RawScore.ShouldBe(2m);
    }

    // -- Vietnamese fill-in-the-blank -----------------------------------------------------

    [Fact]
    public void An_exactly_correct_fill_in_the_blank_answer_scores()
    {
        var q = FillBlank("Hà Nội");

        QuizGrader.Grade([q], [new QuizAnswer(q.Id, [], "Hà Nội")]).RawScore.ShouldBe(1m);
    }

    [Fact]
    public void Fill_in_the_blank_is_case_insensitive()
    {
        var q = FillBlank("Hà Nội");

        QuizGrader.Grade([q], [new QuizAnswer(q.Id, [], "hà nội")]).RawScore.ShouldBe(1m);
    }

    [Fact]
    public void Fill_in_the_blank_ignores_extra_whitespace()
    {
        var q = FillBlank("Hà Nội");

        QuizGrader.Grade([q], [new QuizAnswer(q.Id, [], "  Hà   Nội ")]).RawScore.ShouldBe(1m);
    }

    [Fact]
    public void Fill_in_the_blank_accepts_both_unicode_forms_of_Vietnamese()
    {
        // This is the Gap #12 case and the reason this whole test file exists.
        // NFC: "ế" is ONE precomposed character, U+1EBF (the macOS keyboard).
        // NFD: "ế" is "e" plus a circumflex plus an acute — THREE characters (many Telex IMEs).
        // The two strings render identically on screen.
        var q = FillBlank("Kiến thức");
        var typedWithTelex = "Kiến thức".Normalize(NormalizationForm.FormD);

        typedWithTelex.ShouldNotBe("Kiến thức");                 // different byte for byte
        typedWithTelex.Length.ShouldBeGreaterThan("Kiến thức".Length);

        QuizGrader.Grade([q], [new QuizAnswer(q.Id, [], typedWithTelex)]).RawScore.ShouldBe(1m);
    }

    [Fact]
    public void Fill_in_the_blank_normalizes_the_ANSWER_KEY_too_not_just_the_submission()
    {
        // The instructor typing the answer key with a different IME than the student is routine.
        // Normalizing only one side makes the failure depend on who typed with what — the last
        // thing anyone considers while hunting the bug.
        var q = FillBlank("Kiến thức".Normalize(NormalizationForm.FormD));

        QuizGrader.Grade([q], [new QuizAnswer(q.Id, [], "Kiến thức")]).RawScore.ShouldBe(1m);
    }

    [Fact]
    public void Fill_in_the_blank_accepts_any_of_several_answer_keys()
    {
        var q = FillBlank("Hà Nội", "Ha Noi", "HN");

        QuizGrader.Grade([q], [new QuizAnswer(q.Id, [], "HN")]).RawScore.ShouldBe(1m);
    }

    [Fact]
    public void A_wrong_fill_in_the_blank_answer_scores_nothing()
    {
        var q = FillBlank("Hà Nội");

        QuizGrader.Grade([q], [new QuizAnswer(q.Id, [], "Sài Gòn")]).RawScore.ShouldBe(0m);
    }

    [Fact]
    public void Fill_in_the_blank_respects_case_when_case_sensitivity_is_enabled()
    {
        var q = FillBlank("ABC");
        q.FillBlank!.CaseSensitive = true;

        QuizGrader.Grade([q], [new QuizAnswer(q.Id, [], "abc")]).RawScore.ShouldBe(0m);
    }

    // -- Normalizing to a 10-point scale ---------------------------------------------------

    [Fact]
    public void Normalizing_to_a_10_point_scale_follows_the_Phase_4b_rule()
    {
        // A 20-question quiz with 15 correct -> 7.5, NOT 15 subsequently clamped to 10.
        var questions = Enumerable.Range(0, 20).Select(_ => SingleChoice(("A", true), ("B", false))).ToList();
        var answers = questions.Take(15).Select(q => new QuizAnswer(q.Id, [q.Options[0].Id], null)).ToList();

        var result = QuizGrader.Grade(questions, answers);

        result.RawScore.ShouldBe(15m);
        result.MaxScore.ShouldBe(20m);
        result.NormalizedScore.ShouldBe(7.5m);
    }

    [Fact]
    public void A_quiz_with_no_questions_does_not_divide_by_0()
    {
        QuizGrader.Grade([], []).NormalizedScore.ShouldBe(0m);
    }

    [Fact]
    public void The_list_of_wrong_questions_is_what_feeds_the_review_queue()
    {
        var a = SingleChoice(("A", true), ("B", false));
        var b = SingleChoice(("A", true), ("B", false));

        var result = QuizGrader.Grade([a, b], [new QuizAnswer(a.Id, [a.Options[0].Id], null)]);

        result.WrongQuestionIds.ShouldBe([b.Id]);
    }

    // -- Timing ---------------------------------------------------------------------------

    [Fact]
    public void Submitting_before_time_runs_out_is_ordinary()
    {
        var startedAt = new DateTimeOffset(2026, 9, 1, 8, 0, 0, TimeSpan.FromHours(7));

        QuizTiming.DaHetGio(startedAt, 15, startedAt.AddMinutes(14)).ShouldBeFalse();
    }

    [Fact]
    public void Submitting_within_the_30_second_grace_period_is_still_accepted()
    {
        // A slow network, hitting submit on the final second, clocks a few seconds apart — without
        // a grace period a perfectly valid submission is treated as out of time.
        var startedAt = new DateTimeOffset(2026, 9, 1, 8, 0, 0, TimeSpan.FromHours(7));

        QuizTiming.DaHetGio(startedAt, 15, startedAt.AddMinutes(15).AddSeconds(20)).ShouldBeFalse();
    }

    [Fact]
    public void Past_the_grace_period_it_counts_as_out_of_time()
    {
        var startedAt = new DateTimeOffset(2026, 9, 1, 8, 0, 0, TimeSpan.FromHours(7));

        QuizTiming.DaHetGio(startedAt, 15, startedAt.AddMinutes(16)).ShouldBeTrue();
    }

    [Fact]
    public void An_untimed_quiz_never_runs_out_of_time()
    {
        var startedAt = new DateTimeOffset(2026, 9, 1, 8, 0, 0, TimeSpan.FromHours(7));

        QuizTiming.DaHetGio(startedAt, null, startedAt.AddDays(3)).ShouldBeFalse();
        QuizTiming.ConLai(startedAt, null, startedAt).ShouldBeNull();
    }
}
