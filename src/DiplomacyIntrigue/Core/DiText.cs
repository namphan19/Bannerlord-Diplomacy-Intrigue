using System;
using System.Globalization;
using System.Text;
using TaleWorlds.Localization;

namespace DiplomacyIntrigue.Core
{
    /// <summary>
    /// The one place a player-facing string is written.
    ///
    /// A string the player can read is written as
    /// <c>DiText.T("DI_realm_tab", "Realm")</c> - the English stays in the source as the fallback,
    /// and the key is what a translation looks up. The engine resolves the key against the
    /// language the game is running in and falls back to the English when there is no entry
    /// (<c>MBTextManager.ProcessTextToString</c> takes the default text when
    /// <c>GetLocalizedText</c> comes back empty), so a missing translation degrades to English and
    /// never to a blank or a raw key (story 4.1 R1).
    ///
    /// Why a helper call and not <c>new TextObject("{=key}Text")</c> at each site: a Gauntlet
    /// widget's <c>Text</c> is a plain string. <c>TextWidget.SetText</c> hands it to
    /// <c>TwoDimension.Text.Value</c> and <c>TextParser.Parse</c> never looks at a key, so the
    /// resolution has to happen here, in the view model, before the string reaches the widget.
    /// Verified by IL against v1.5.3 on 2026-10-03, not by a session - see the story's §9.
    ///
    /// Two rules the callers must keep:
    ///  - A sentence with a number or a name in it is ONE call with named variables, never a
    ///    concatenation (<see cref="T(string,string,object[])"/> R3). Word order is not the same
    ///    in every language, and a concatenation fixes it in English for good.
    ///  - A value is passed as a variable, never typed into the English. A number on screen stays
    ///    the number the AI used (CLAUDE.md §3); translation changes words, never values.
    /// </summary>
    internal static class DiText
    {
        /// <summary>
        /// The two characters that open a localization id in the engine's own format: a brace and
        /// an equals sign, then the key, then the brace that closes it - <c>{=DI_REALM_REALM}Realm</c>.
        ///
        /// This was <c>"{" + KeyPrefix + "}" + key</c>, which builds <c>{DI_}DI_REALM_REALM</c>:
        /// the prefix sat inside the braces instead of in front of the key, so the engine read a
        /// token with no sigil and no id at all. Nothing local noticed: the English file held the
        /// key, the check read key and English and passed, and every screen drew
        /// <c>{DI_}DI_REALM_REALM_2}Realm</c> where it should have drawn "Realm". Found in game on
        /// 2026-10-04, the first session after the conversion - which is the argument for ST-7.
        ///
        /// Every key starts with <c>DI_</c>, so one of ours can never shadow a vanilla string id.
        /// <c>scripts/check-localization.ps1</c> refuses a key without it, rule 7; this class does
        /// not repeat the rule, it only builds the string.
        /// </summary>
        internal const string Open = "{=";

        /// <summary>
        /// How many <see cref="English"/> scopes the current thread is inside. While it is above
        /// zero <see cref="T"/> and <see cref="O"/> answer the English the call carries and never
        /// look a key up.
        ///
        /// Why this exists: the same producers (<c>ExhaustionBands.Name</c>, <c>CourtBands.Name</c>,
        /// <c>Treaty.NameOf</c>...) feed both the screens and the log, the telemetry and the
        /// <c>diplomacy.*</c> commands. The second group is read by tools such as
        /// <c>analyse-log.py</c> and by the lead, and story 4.1 §3 keeps it English. Without a
        /// scope a log line written under a Deutsch game would say "Gereizt" in the middle of an
        /// English sentence. Thread-static, because the engine's UI and the campaign tick are one
        /// thread but a command may be called from the bridge's thread.
        /// </summary>
        [ThreadStatic] private static int _englishDepth;

        /// <summary>
        /// Everything built inside the returned scope is English: <c>using (DiText.English()) ...</c>
        /// or <c>using var _ = DiText.English();</c> at the top of a <c>diplomacy.*</c> command.
        /// Text shown to a player must never be built inside one.
        /// </summary>
        internal static IDisposable English()
        {
            _englishDepth++;
            return new EnglishScope();
        }

        private sealed class EnglishScope : IDisposable
        {
            private bool _done;
            public void Dispose()
            {
                if (_done) return;
                _done = true;
                if (_englishDepth > 0) _englishDepth--;
            }
        }

        /// <summary>
        /// The localized text as a <see cref="TextObject"/>, for the engine's own signatures that
        /// want one (<c>InformationNotification</c>, <c>GameTexts</c>, a variable set on a prompt).
        /// </summary>
        internal static TextObject O(string key, string english, params (string Name, object Value)[] vars)
        {
            // Under an English scope the id is left out, so the engine has nothing to look up and
            // takes the English the call carries.
            var text = new TextObject(_englishDepth > 0 ? english : Open + key + "}" + english);
            for (var i = 0; i < vars.Length; i++)
                text.SetTextVariable(vars[i].Name, AsText(vars[i].Value));
            return text;
        }

        /// <summary>
        /// The localized text as a string, which is what a view-model property and a prefab's
        /// <c>Text="@Property"</c> binding take.
        /// </summary>
        internal static string T(string key, string english, params (string Name, object Value)[] vars)
        {
            try
            {
                return O(key, english, vars).ToString();
            }
            catch
            {
                var fallback = Substitute(english, vars);
                // Text lookup failing must not take a screen down with it (R6). Localization is
                // not up during the earliest SubModule hooks, and a broken language file must
                // read as English rather than as an empty panel.
                return fallback;
            }
        }

        /// <summary>
        /// Renders a value the way string concatenation did, which is the point: the English has to
        /// come out character for character what it did before the key was added (AC1).
        /// <see cref="TextObject.SetTextVariable(string,int)"/> would run the value through the
        /// active language's number format instead, which is a different string.
        /// </summary>
        private static string AsText(object value)
        {
            if (value == null) return string.Empty;
            if (value is string s) return s;
            return Convert.ToString(value, CultureInfo.CurrentCulture);
        }

        /// <summary>
        /// The English with its variables filled in, used when the localization lookup cannot run.
        /// Deliberately naive: it is a safety net, not a second formatter, and a name that happens
        /// to contain braces is a translator's problem to avoid.
        /// </summary>
        private static string Substitute(string english, (string Name, object Value)[] vars)
        {
            if (vars.Length == 0 || string.IsNullOrEmpty(english)) return english;

            var sb = new StringBuilder(english);
            for (var i = 0; i < vars.Length; i++)
            {
                var placeholder = "{" + vars[i].Name + "}";
                var at = sb.ToString().IndexOf(placeholder, StringComparison.Ordinal);
                while (at >= 0)
                {
                    sb.Remove(at, placeholder.Length).Insert(at, AsText(vars[i].Value));
                    at = sb.ToString().IndexOf(placeholder, at + 1, StringComparison.Ordinal);
                }
            }
            return sb.ToString();
        }
    }
}
