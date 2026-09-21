using Airside.Domain;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class BuildIdentityTests
    {
        private const string Sample =
            "commit=927634e1\n" +
            "commitFull=927634e1aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\n" +
            "branch=main\n" +
            "subject=Merge pull request #352 from Bazlinka/example\n" +
            "committedAt=2026-09-21T04:12:00+09:30\n" +
            "dirty=false\n" +
            "stampedAt=2026-09-21T04:30:00Z\n";

        [Test]
        public void Parse_ReadsTheStampScriptKeys()
        {
            var identity = BuildIdentity.Parse(Sample);

            Assert.That(identity.IsStamped, Is.True);
            Assert.That(identity.Commit, Is.EqualTo("927634e1"));
            Assert.That(identity.CommitFull, Is.EqualTo("927634e1aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"));
            Assert.That(identity.Branch, Is.EqualTo("main"));
            Assert.That(identity.Subject, Is.EqualTo("Merge pull request #352 from Bazlinka/example"));
            Assert.That(identity.Dirty, Is.False);
            Assert.That(identity.ShortLabel, Is.EqualTo("927634e1 · main"));
            Assert.That(identity.FullLabel, Is.EqualTo(
                "927634e1 · main · committed 2026-09-20 18:42 UTC · built 2026-09-21 04:30 UTC"));
        }

        [Test]
        public void Parse_RoundTripsThroughTheCanonicalFileText()
        {
            var identity = BuildIdentity.Parse(Sample);
            var again = BuildIdentity.Parse(identity.ToFileText());

            Assert.That(again.Commit, Is.EqualTo(identity.Commit));
            Assert.That(again.CommitFull, Is.EqualTo(identity.CommitFull));
            Assert.That(again.Branch, Is.EqualTo(identity.Branch));
            Assert.That(again.Subject, Is.EqualTo(identity.Subject));
            Assert.That(again.CommittedAt, Is.EqualTo(identity.CommittedAt));
            Assert.That(again.Dirty, Is.EqualTo(identity.Dirty));
            Assert.That(again.StampedAt, Is.EqualTo(identity.StampedAt));
            Assert.That(again.ShortLabel, Is.EqualTo(identity.ShortLabel));
        }

        [Test]
        public void Parse_DirtyTreeAddsLocalChanges()
        {
            var identity = BuildIdentity.Parse(Sample.Replace("dirty=false", "dirty=TRUE"));

            Assert.That(identity.Dirty, Is.True);
            Assert.That(identity.ShortLabel, Is.EqualTo("927634e1 · main · local changes"));
            Assert.That(identity.FullLabel, Does.StartWith("927634e1 · main · local changes · committed "));
        }

        [Test]
        public void Parse_SubjectMayContainEquals()
        {
            var identity = BuildIdentity.Parse("commit=abc12345\nsubject=fix a=b in the board\ndirty=0\n");

            Assert.That(identity.Subject, Is.EqualTo("fix a=b in the board"));
            Assert.That(identity.Dirty, Is.False);
            Assert.That(identity.ShortLabel, Is.EqualTo("abc12345"));
        }

        [Test]
        public void Parse_MissingOrBlankIsUnstamped()
        {
            Assert.That(BuildIdentity.Parse(null).ShortLabel, Is.EqualTo(BuildIdentity.UnstampedLabel));
            Assert.That(BuildIdentity.Parse("").FullLabel, Is.EqualTo(BuildIdentity.UnstampedLabel));
            Assert.That(BuildIdentity.Parse("branch=main\ndirty=true\n").IsStamped, Is.False);
            Assert.That(BuildIdentity.Parse("# comment\ncommit=\n").IsStamped, Is.False);
        }
    }
}
