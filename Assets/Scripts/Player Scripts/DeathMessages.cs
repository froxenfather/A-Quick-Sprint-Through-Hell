using UnityEngine;

// one death message: the big title, the smaller tip under it, and the title's color.
// the block font (Anton) is used when UseVoidFont is true, otherwise the scary font from PlayerDeath's Stats screen.
// ShrinkToFit (true unless a message says otherwise): a title or tip too long for its box shrinks until it fits.
// give a message shrinkToFit: false and its text keeps its full size and runs over the screen
public struct DeathMessage
{
    public string Title;
    public Color Color;
    public string Tip;
    public bool UseVoidFont;
    public bool ShrinkToFit;

    public DeathMessage(string title, Color color, string tip, bool useVoidFont = false, bool shrinkToFit = true)
    {
        Title = title;
        Color = color;
        Tip = tip;
        UseVoidFont = useVoidFont;
        ShrinkToFit = shrinkToFit;
    }
}

// everything the death screen says, found by the NAME of the death.
// PlayerDeath works out which death happened and calls Get with its name.
// to add a death: add a name below, add a case for it in Get, and make PlayerDeath.PickConseraphDeath return the name when it should happen.
// the titles, tips and colors are not in the Inspector, change them here
public static class DeathMessages
{
    // ============================================================
    // THE NAMES
    // ============================================================

    public const string Void = "Void";                 // lost to the void ("Let Go")
    public const string Mania = "Mania";               // killed by Conseraph while he is manic
    public const string Parried10 = "Parried10";       // killed by him after parrying him 10 times or more
    public const string Parried5 = "Parried5";         // 5 times or more
    public const string Parried1 = "Parried1";         // at least once
    public const string Enraged = "Enraged";           // killed by him while he was enraged, never parried
    public const string Singed = "Singed";             // killed by him while singed
    public const string Normal = "Normal";             // killed by him, nothing special

    // ============================================================
    // THE COLORS
    // ============================================================

    private static readonly Color Red = Color.red;
    private static readonly Color Purple = new Color(0.6f, 0.2f, 1f);

    // ============================================================
    // THE MESSAGES
    // ============================================================

    public static DeathMessage Get(string deathName)
    {
        switch (deathName)
        {
            case Void:
                return new DeathMessage(
                    "The Void consumes you.",
                    Purple,
                    "Make sure to stay inside the Eternal Factory and avoid holes!",
                    true);

            // the title and tip are long ON PURPOSE and do not shrink (shrinkToFit: false): they keep their full size and run over the whole screen
            case Mania:
                return new DeathMessage(
                    "IHATEYOUIHATEYOUIHATEYOUIHATEYOUIHATEYOUIHATEYOUIHATEYOUIHATEYOUIHATEYOUIHATEYOUIHATEYOUIHATEYOUIHATEYOUIHATEYOUIHATEYOUIHATEYOUIHATEYOUIHATEYOUIHATEYOUIHATEYOUIHATEYOUIHATEYOUIHATEYOUIHATEYOUIHATEYOUIHATEYOUIHATEYOUIHATEYOUIHATEYOUIHATEYOUIHATEYOUIHATEYOUIHATEYOUIHATEYOUIHATEYOUIHATEYOUIHATEYOUIHATEYOUIHATEYOUIHATEYOUIHATEYOUIHATEYOUIHATEYOUIHATEYOUIHATEYOUIHATEYOUIHATEYOUIHATEYOUIHATEYOUIHATEYOUIHATEYOUIHATEYOUIHATEYOUIHATEYOUIHATEYOUIHATEYOUIHATEYOUIHATEYOUIHATEYOUIHATEYOU",
                    Red,
                    "NOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUNNOWHERETORUN",
                    shrinkToFit: false);

            case Parried10:
                return new DeathMessage(
                    "YOU LITTLE SHIT",
                    Red,
                    "EVERY DENIAL MAKES ME ENJOY THIS MORE. GIVE IN OR ILL TEAR YOUR SOUL OUT.");

            case Parried5:
                return new DeathMessage(
                    "You Are No Match For Me",
                    Red,
                    "Why do you fight this? Just give in! Stop it.");

            case Parried1:
                return new DeathMessage(
                    "Conseraph Killed You.",
                    Red,
                    "You think yourself clever don't you? :D");

            case Enraged:
                return new DeathMessage(
                    "You were torn asunder by Conseraph.",
                    Red,
                    "You were alive too long anyway.");

            case Singed:
                return new DeathMessage(
                    "You succumbed to Conseraph",
                    Red,
                    "When singed or in lava, slide bouncing and dashing is the best way to keep speed and get out!");

            case Normal:
                return new DeathMessage(
                    "You gave in to Conseraph",
                    Red,
                    "Try taking your time to ensure you do not need to backtrack or slow down!");

            default:
                Debug.LogWarning($"DeathMessages has no death called '{deathName}', so the normal Conseraph death is used. Add a case for it in DeathMessages.Get.");
                return Get(Normal);
        }
    }

    // ============================================================
    // THE MANIA PHRASES
    // ============================================================

    // dying the Mania death: every stat's number is replaced by one of these, picked at random for each stat.
    // the labels (Rooms cleared, Top speed...) stay. add, change or remove any of them
    public static readonly string[] ManiaPhrases =
    {
        "HATRED",
        "IHATEYOU",
        "LIAR",
        "DECIEVER",
        "YOUAREALONE",
        "ANGER",
        "PERISH",
        "FOREVERALONE",
        "USELESS",
        "ISEEYOURSINS",
        "NOLOVE",
        "ABSENCE",
        "SUBMIT",
        "FALL",
        "DIE",
        "NO GOD NOW"
    };
}
