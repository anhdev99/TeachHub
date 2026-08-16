using Shouldly;
using TeachHub.Application.Features.Learning;

namespace TeachHub.Application.Tests;

/// <summary>
/// The rule for grading inline questions has to live in EXACTLY ONE place, because it is read
/// from two directions: the path that returns the lesson payload (the <c>multi_select</c> flag)
/// and the path that grades. Two copies will drift apart on precisely the day someone adds a
/// second correct answer to a question — and the symptom is that question grading wrong for the
/// entire class with no error anywhere in the logs.
/// </summary>
public class InlineQuizGraderTests
{
    [Fact]
    public void One_correct_answer_does_not_make_it_a_multi_select_question()
    {
        InlineQuizGrader.IsMultiSelect([1]).ShouldBeFalse();
    }

    [Fact]
    public void Two_or_more_correct_answers_make_it_a_multi_select_question()
    {
        InlineQuizGrader.IsMultiSelect([0, 2]).ShouldBeTrue();
    }

    [Fact]
    public void Picking_the_one_right_option_is_correct()
    {
        InlineQuizGrader.IsCorrect([1], [1]).ShouldBeTrue();
    }

    [Fact]
    public void Picking_the_wrong_option_is_incorrect()
    {
        InlineQuizGrader.IsCorrect([1], [0]).ShouldBeFalse();
    }

    [Fact]
    public void Selecting_two_options_on_a_single_answer_question_is_WRONG_not_half_right()
    {
        // The same rule as QuizGrader's single-choice grading in Phase 5: silently accepting the
        // first option would be going easy on someone guessing blindly.
        InlineQuizGrader.IsCorrect([1], [0, 1]).ShouldBeFalse();
    }

    [Fact]
    public void A_multi_select_question_requires_EVERY_correct_answer()
    {
        InlineQuizGrader.IsCorrect([0, 2], [0]).ShouldBeFalse();
        InlineQuizGrader.IsCorrect([0, 2], [0, 2]).ShouldBeTrue();
        // The order they arrive in means nothing: this is a set, not a list.
        InlineQuizGrader.IsCorrect([0, 2], [2, 0]).ShouldBeTrue();
    }

    [Fact]
    public void Picking_every_correct_answer_plus_one_wrong_option_is_still_WRONG()
    {
        InlineQuizGrader.IsCorrect([0, 2], [0, 1, 2]).ShouldBeFalse();
    }

    [Fact]
    public void Not_answering_is_incorrect_rather_than_an_explosion()
    {
        InlineQuizGrader.IsCorrect([1], null).ShouldBeFalse();
        InlineQuizGrader.IsCorrect([1], []).ShouldBeFalse();
    }

    [Fact]
    public void Selecting_the_same_option_twice_does_not_change_the_outcome()
    {
        // The client sending [1,1] happens when the user double-clicks; that is still "ticked box 1".
        InlineQuizGrader.IsCorrect([1], [1, 1]).ShouldBeTrue();
    }

    [Fact]
    public void A_question_authored_without_a_correct_answer_is_never_graded_correct()
    {
        // Corrupt data from an incomplete authoring pass. Grading every submission "correct" here
        // is the quietest possible way for a meaningless question to unlock the next lesson.
        InlineQuizGrader.IsCorrect([], []).ShouldBeFalse();
        InlineQuizGrader.IsCorrect([], [0]).ShouldBeFalse();
    }
}
