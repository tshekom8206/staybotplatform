using System;
using System.Collections.Generic;
using System.Linq;

namespace Hostr.Api.Services
{
    public interface IMessageNormalizationService
    {
        string NormalizeMessage(string message);
        string ExpandAbbreviations(string message);
        string NormalizeHotelTerms(string message);
    }

    public class MessageNormalizationService : IMessageNormalizationService
    {
        private readonly ILogger<MessageNormalizationService> _logger;

        // Direct mapping for common misspellings and abbreviations
        // This is much more scalable than fuzzy matching - just maintain this dictionary
        private readonly Dictionary<string, string> _termNormalizations = new()
        {
            // Common menu misspellings
            { "manu", "menu" },
            { "menue", "menu" },
            { "meanu", "menu" },
            { "meniu", "menu" },
            { "meni", "menu" },

            // Room related
            { "rooom", "room" },
            { "rom", "room" },
            { "romm", "room" },
            { "roon", "room" },

            // Towels
            { "towl", "towel" },
            { "towles", "towels" },
            { "towell", "towel" },
            { "towells", "towels" },
            { "towerls", "towels" },

            // Bathroom items
            { "bathrrom", "bathroom" },
            { "bathrom", "bathroom" },
            { "batroom", "bathroom" },
            { "tolet", "toilet" },
            { "toilett", "toilet" },

            // WiFi variations
            { "wi-fi", "wifi" },
            { "wi fi", "wifi" },
            { "wify", "wifi" },
            { "wiifi", "wifi" },

            // Service variations
            { "servise", "service" },
            { "servis", "service" },
            { "servic", "service" },

            // Common abbreviations
            { "plz", "please" },
            { "pls", "please" },
            { "thx", "thanks" },
            { "ty", "thank you" },
            { "ur", "your" },
            { "u", "you" },
            { "r", "are" },
            { "w/", "with" },
            { "w/o", "without" },
            { "asap", "as soon as possible" },

            // Numbers (common in hotel requests)
            { "1", "one" },
            { "2", "two" },
            { "3", "three" },
            { "4", "four" },
            { "5", "five" },
            { "won", "one" },
            // REMOVED: { "to", "two" }, - conflicts with preposition "to"
            // REMOVED: { "too", "two" }, - conflicts with adverb "too"
            { "tree", "three" },
            // REMOVED: { "for", "four" }, - conflicts with preposition "for"
            { "fiv", "five" },

            // Time related
            { "tody", "today" },
            { "todya", "today" },
            { "tomoro", "tomorrow" },
            { "tomorow", "tomorrow" },
            { "tommorow", "tomorrow" },
            { "nw", "now" },

            // Common typos
            { "teh", "the" },
            { "adn", "and" },
            { "cna", "can" },
            { "nede", "need" },
            { "ned", "need" },
            { "wnt", "want" },
            { "cn", "can" },
            { "coud", "could" },
            { "wuld", "would" },
            { "woud", "would" },

            // Hotel items
            { "shampo", "shampoo" },
            { "conditionr", "conditioner" },
            { "irn", "iron" },
            { "chargr", "charger" },
            { "charjer", "charger" },

            // Polite expressions
            { "thnks", "thanks" },
            { "thks", "thanks" },
            { "plez", "please" },
            { "pleas", "please" },
            { "sory", "sorry" },
            { "soory", "sorry" },
            { "soryr", "sorry" },

            // Cleaning related
            { "cleanning", "cleaning" },
            { "clening", "cleaning" },
            { "clen", "clean" },

            // Check-in/out normalization
            { "checkin", "check-in" },
            { "check in", "check-in" },
            { "checkout", "check-out" },
            { "check out", "check-out" },

            // Reception variations
            { "recetion", "reception" },
            { "recepion", "reception" },
            { "resepton", "reception" },

            // Breakfast variations
            { "breakfst", "breakfast" },
            { "brekfast", "breakfast" },
            { "brakfast", "breakfast" }
        };

        // Multi-word phrase normalizations
        private readonly Dictionary<string, string> _phraseNormalizations = new()
        {
            { "toilet papers", "toilet paper" },
            { "tolet paper", "toilet paper" },
            { "towel's", "towels" },
            { "can i have", "I need" },
            { "could you bring", "I need" },
            { "would you please bring", "I need" },
            { "can you send", "I need" },
            { "i want", "I need" },
            { "give me", "I need" },
            { "send me", "I need" }
        };

        public MessageNormalizationService(ILogger<MessageNormalizationService> logger)
        {
            _logger = logger;
        }

        public string NormalizeMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return message;

            try
            {
                var normalized = message;

                // Step 1: Normalize phrases first (multi-word)
                normalized = NormalizePhrases(normalized);

                // Step 2: Normalize individual terms
                normalized = NormalizeHotelTerms(normalized);

                // Step 3: Expand common abbreviations
                normalized = ExpandAbbreviations(normalized);

                if (normalized != message)
                {
                    _logger.LogInformation("Message normalized: '{Original}' -> '{Normalized}'", message, normalized);
                }

                return normalized;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during message normalization: {Message}", message);
                return message; // Return original on error
            }
        }

        public string ExpandAbbreviations(string message)
        {
            // This is already handled in NormalizeMessage, but kept for interface compatibility
            return message;
        }

        public string NormalizeHotelTerms(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return message;

            var words = message.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var normalizedWords = new List<string>();

            foreach (var word in words)
            {
                // Preserve punctuation
                var cleanWord = word.ToLower().Trim('.', ',', '!', '?', ';', ':', '"', '\'');
                var punctuation = word.Length > cleanWord.Length ? word.Substring(cleanWord.Length) : "";

                // Direct lookup - O(1) performance, highly scalable
                if (_termNormalizations.TryGetValue(cleanWord, out var normalized))
                {
                    normalizedWords.Add(normalized + punctuation);
                }
                else
                {
                    normalizedWords.Add(word);
                }
            }

            return string.Join(" ", normalizedWords);
        }

        private string NormalizePhrases(string message)
        {
            var normalized = message.ToLower();

            // Direct phrase replacement - much more efficient than pattern matching
            foreach (var phrase in _phraseNormalizations)
            {
                if (normalized.Contains(phrase.Key))
                {
                    normalized = normalized.Replace(phrase.Key, phrase.Value);
                }
            }

            return normalized;
        }

        /// <summary>
        /// Adds a new term normalization. This can be called to extend the dictionary dynamically
        /// </summary>
        public void AddTermNormalization(string incorrectTerm, string correctTerm)
        {
            if (!string.IsNullOrWhiteSpace(incorrectTerm) && !string.IsNullOrWhiteSpace(correctTerm))
            {
                _termNormalizations[incorrectTerm.ToLower()] = correctTerm.ToLower();
                _logger.LogInformation("Added term normalization: '{Incorrect}' -> '{Correct}'", incorrectTerm, correctTerm);
            }
        }

        /// <summary>
        /// Gets normalization statistics for monitoring purposes
        /// </summary>
        public (int TermCount, int PhraseCount) GetNormalizationStats()
        {
            return (_termNormalizations.Count, _phraseNormalizations.Count);
        }
    }
}