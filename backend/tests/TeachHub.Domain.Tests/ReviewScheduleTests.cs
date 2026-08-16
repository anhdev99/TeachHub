using Shouldly;
using TeachHub.Domain.Review;

namespace TeachHub.Domain.Tests;

/// <summary>
/// The spaced-repetition ladder: 3 days -> 1 week -> 2 weeks -> mastered. A wrong answer at ANY
/// rung sends the item back to the start of the cycle.
///
/// This is the most expensive rule in Phase 8 and also the one that CANNOT be checked by eye in
/// the UI: watching a single item walk the whole ladder by hand would take 24 real days. That is
/// why the entire rule is a pure function taking <c>now</c>, and why this whole file runs in
/// under 20 ms.
/// </summary>
public class ReviewScheduleTests
{
    // A fixed instant carrying a +07 offset: if the rule ever used DateTime instead of
    // DateTimeOffset, the day arithmetic would be off by exactly 7 hours and this test catches it.
    private static readonly DateTimeOffset Start = new(2026, 9, 1, 8, 0, 0, TimeSpan.FromHours(7));

    // -- First rung ----------------------------------------------------------------------

    [Fact]
    public void An_item_born_from_a_wrong_answer_is_due_for_review_in_3_days()
    {
        var result = ReviewSchedule.Start(Start);

        result.Stage.ShouldBe(ReviewStage.ThreeDays);
        result.NextReviewAt.ShouldBe(Start.AddDays(3));
        result.IsMastered.ShouldBeFalse();
    }

    // -- The full four-rung ladder -------------------------------------------------------

    [Fact]
    public void The_ladder_walks_3_days_then_1_week_then_2_weeks_then_mastered()
    {
        var rung1 = ReviewSchedule.Start(Start);
        rung1.NextReviewAt.ShouldBe(Start.AddDays(3));

        var review1 = rung1.NextReviewAt!.Value;
        var rung2 = ReviewSchedule.Next(rung1.Stage, traLoiDung: true, review1);
        rung2.Stage.ShouldBe(ReviewStage.OneWeek);
        rung2.NextReviewAt.ShouldBe(review1.AddDays(7));

        var review2 = rung2.NextReviewAt!.Value;
        var rung3 = ReviewSchedule.Next(rung2.Stage, traLoiDung: true, review2);
        rung3.Stage.ShouldBe(ReviewStage.TwoWeeks);
        rung3.NextReviewAt.ShouldBe(review2.AddDays(14));

        var review3 = rung3.NextReviewAt!.Value;
        var rung4 = ReviewSchedule.Next(rung3.Stage, traLoiDung: true, review3);
        rung4.Stage.ShouldBe(ReviewStage.Mastered);
        rung4.IsMastered.ShouldBeTrue();

        // Once mastered there is NO next review date — that is what keeps the item out of the
        // queue. Leaving an old date here means a mastered item shows up under "Review today"
        // forever.
        rung4.NextReviewAt.ShouldBeNull();
    }

    // -- A wrong answer: back to the start of the cycle -----------------------------------

    [Theory]
    [InlineData(ReviewStage.ThreeDays)]
    [InlineData(ReviewStage.OneWeek)]
    [InlineData(ReviewStage.TwoWeeks)]
    public void A_wrong_answer_at_ANY_rung_returns_to_3_days(ReviewStage currentStage)
    {
        var result = ReviewSchedule.Next(currentStage, traLoiDung: false, Start);

        result.Stage.ShouldBe(ReviewStage.ThreeDays);
        result.NextReviewAt.ShouldBe(Start.AddDays(3));
    }

    [Fact]
    public void A_wrong_answer_at_the_LAST_rung_does_not_master_it_but_returns_to_3_days()
    {
        // The easiest case to get wrong: one step past the last rung is "mastered", so a switch
        // branch missing the correct/incorrect condition hands the student "mastered" at exactly
        // the moment they answered wrong.
        var result = ReviewSchedule.Next(ReviewStage.TwoWeeks, traLoiDung: false, Start);

        result.Stage.ShouldBe(ReviewStage.ThreeDays);
        result.IsMastered.ShouldBeFalse();
        result.NextReviewAt.ShouldBe(Start.AddDays(3));
    }

    [Fact]
    public void Getting_an_already_mastered_concept_wrong_returns_to_the_start_of_the_cycle()
    {
        // Getting an old concept wrong while taking a later subject really happens, and that is
        // precisely why ReviewQueueItem hangs off user_id rather than enrollment_id (Gap #2).
        var result = ReviewSchedule.Next(ReviewStage.Mastered, traLoiDung: false, Start);

        result.Stage.ShouldBe(ReviewStage.ThreeDays);
        result.NextReviewAt.ShouldBe(Start.AddDays(3));
    }

    [Fact]
    public void Answering_correctly_once_more_after_mastery_stays_mastered()
    {
        var result = ReviewSchedule.Next(ReviewStage.Mastered, traLoiDung: true, Start);

        result.Stage.ShouldBe(ReviewStage.Mastered);
        result.NextReviewAt.ShouldBeNull();
    }

    // -- The schedule counts from the ANSWER, not from the old due date --------------------

    [Fact]
    public void Reviewing_late_schedules_the_next_one_from_the_answer_not_from_the_old_due_date()
    {
        // The student ignores the queue for 10 days and only then reviews. Computing the next
        // date from the old due date (Start+3+7) would force them to review again just 4 days
        // later — compressing the schedule exactly when they are already falling behind.
        var lateReview = Start.AddDays(13);

        var result = ReviewSchedule.Next(ReviewStage.ThreeDays, traLoiDung: true, lateReview);

        result.NextReviewAt.ShouldBe(lateReview.AddDays(7));
    }

    // -- Queue items ---------------------------------------------------------------------

    [Fact]
    public void A_new_item_belongs_to_the_LEARNER_and_already_counts_one_wrong_answer()
    {
        var learner = Guid.CreateVersion7();
        var question = Guid.CreateVersion7();
        var attempt = Guid.CreateVersion7();

        var item = ReviewQueueItem.Create(learner, question, attempt, Start);

        item.UserId.ShouldBe(learner);
        item.QuestionId.ShouldBe(question);
        item.SourceQuizAttemptId.ShouldBe(attempt);
        item.Stage.ShouldBe(ReviewStage.ThreeDays);
        item.Status.ShouldBe(ReviewItemStatus.Pending);
        item.NextReviewAt.ShouldBe(Start.AddDays(3));

        // The item exists BECAUSE of a wrong answer — starting the count at 0 loses the very
        // event that created it.
        item.WrongCount.ShouldBe(1);
        item.CorrectCount.ShouldBe(0);
    }

    [Fact]
    public void An_item_is_due_only_once_next_review_at_has_passed()
    {
        var item = ReviewQueueItem.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), null, Start);

        item.IsDue(Start).ShouldBeFalse();
        item.IsDue(Start.AddDays(2).AddHours(23)).ShouldBeFalse();
        item.IsDue(Start.AddDays(3)).ShouldBeTrue();
        item.IsDue(Start.AddDays(30)).ShouldBeTrue();
    }

    [Fact]
    public void A_mastered_item_never_enters_the_queue_again()
    {
        var item = ReviewQueueItem.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), null, Start);

        item.ApplyAnswer(dung: true, Start.AddDays(3));
        item.ApplyAnswer(dung: true, Start.AddDays(10));
        item.ApplyAnswer(dung: true, Start.AddDays(24));

        item.Stage.ShouldBe(ReviewStage.Mastered);
        item.Status.ShouldBe(ReviewItemStatus.Mastered);
        item.NextReviewAt.ShouldBeNull();
        item.CorrectCount.ShouldBe(3);

        // No matter how far the clock is pushed, it never comes due again.
        item.IsDue(Start.AddYears(5)).ShouldBeFalse();
    }

    [Fact]
    public void A_wrong_answer_returns_a_mastered_item_to_the_pending_state()
    {
        var item = ReviewQueueItem.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), null, Start);
        item.ApplyAnswer(dung: true, Start.AddDays(3));
        item.ApplyAnswer(dung: true, Start.AddDays(10));
        item.ApplyAnswer(dung: true, Start.AddDays(24));

        var wrongAgain = Start.AddDays(100);
        item.ApplyAnswer(dung: false, wrongAgain);

        item.Stage.ShouldBe(ReviewStage.ThreeDays);
        item.Status.ShouldBe(ReviewItemStatus.Pending);
        item.NextReviewAt.ShouldBe(wrongAgain.AddDays(3));
        item.WrongCount.ShouldBe(2);          // one at creation, one just now
        item.LastReviewedAt.ShouldBe(wrongAgain);
    }

    // -- The plan's acceptance scenario, written as a test ---------------------------------

    [Fact]
    public void Acceptance_three_wrong_answers_enqueue_three_items_and_the_ladder_runs_correctly()
    {
        var learner = Guid.CreateVersion7();
        var attempt = Guid.CreateVersion7();
        var wrongQuestions = new[] { Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7() };

        var queue = wrongQuestions
            .Select(q => ReviewQueueItem.Create(learner, q, attempt, Start))
            .ToList();

        // 3 items enter the queue with a due date of +3 days.
        queue.Count.ShouldBe(3);
        queue.ShouldAllBe(i => i.NextReviewAt == Start.AddDays(3));

        // Push the clock 3 days -> all 3 appear under "Review today".
        var threeDaysLater = Start.AddDays(3);
        queue.Count(i => i.IsDue(threeDaysLater)).ShouldBe(3);

        // Answering CORRECTLY -> the next schedule is 1 week out.
        queue[0].ApplyAnswer(dung: true, threeDaysLater);
        queue[0].Stage.ShouldBe(ReviewStage.OneWeek);
        queue[0].NextReviewAt.ShouldBe(threeDaysLater.AddDays(7));

        // Answering WRONG -> back to 3 days.
        queue[1].ApplyAnswer(dung: false, threeDaysLater);
        queue[1].Stage.ShouldBe(ReviewStage.ThreeDays);
        queue[1].NextReviewAt.ShouldBe(threeDaysLater.AddDays(3));
    }
}
