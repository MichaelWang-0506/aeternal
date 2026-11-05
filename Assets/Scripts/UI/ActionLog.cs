using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace Aeternal.UI
{
    /// <summary>
    /// Displays a scrolling list of action messages. New messages appear at the bottom and scroll up.
    /// Messages automatically fade out and disappear after a set duration.
    /// </summary>
    public class ActionLog : MonoBehaviour
    {
        [Header("UI References")]
        [Tooltip("Parent transform where log entries will be created")]
        public Transform logParent;
        
        [Tooltip("Prefab for a single log entry (should have TMP_Text component)")]
        public GameObject logEntryPrefab;

        [Header("Settings")]
        [Tooltip("How long each message stays visible before fading out (in seconds)")]
        public float messageLifetime = 5f;
        
        [Tooltip("Fade out duration (in seconds)")]
        public float fadeDuration = 1f;
        
        [Tooltip("Maximum number of messages to keep visible")]
        public int maxMessages = 10;

        [Tooltip("Font size of new entries (0 keeps the prefab's). Set by BattleUISkin.")]
        public float entryFontSize = 0f;
        [Tooltip("Colour of new entries (alpha 0 keeps the prefab's). Set by BattleUISkin.")]
        public Color entryColor = new Color(1f, 1f, 1f, 0f);

        private List<LogEntry> activeEntries = new List<LogEntry>();

        private class LogEntry
        {
            public GameObject gameObject;
            public TMP_Text text;
            public CanvasGroup canvasGroup;
            public float spawnTime;
            public bool isFading;

            public LogEntry(GameObject go, TMP_Text txt, CanvasGroup cg)
            {
                gameObject = go;
                text = txt;
                canvasGroup = cg;
                spawnTime = Time.time;
                isFading = false;
            }
        }

        private void Update()
        {
            UpdateLogEntries();
        }

        /// <summary>
        /// Static reference for easy access (set in Start/Awake)
        /// </summary>
        private static ActionLog instance;
        public static ActionLog Instance => instance;

        private void Awake()
        {
            instance = this;
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        /// <summary>
        /// Adds a new message to the action log
        /// </summary>
        public void AddMessage(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            if (logParent == null || logEntryPrefab == null)
            {
                Debug.LogWarning("[ActionLog] logParent or logEntryPrefab not assigned!");
                return;
            }

            // Create new log entry
            var entryObj = Instantiate(logEntryPrefab, logParent);
            var text = entryObj.GetComponent<TMP_Text>();
            var canvasGroup = entryObj.GetComponent<CanvasGroup>();
            
            if (canvasGroup == null)
            {
                canvasGroup = entryObj.AddComponent<CanvasGroup>();
            }

            if (text != null)
            {
                text.text = message;
                if (entryFontSize > 0f)
                {
                    text.enableAutoSizing = false;
                    text.fontSize = entryFontSize;
                }
                if (entryColor.a > 0f) text.color = entryColor;
            }

            var entry = new LogEntry(entryObj, text, canvasGroup);
            activeEntries.Add(entry);

            // Move new entry to bottom (last sibling)
            entryObj.transform.SetAsLastSibling();

            // Remove oldest entries if we exceed max
            while (activeEntries.Count > maxMessages)
            {
                RemoveOldestEntry();
            }

            Debug.Log($"[ActionLog] {message}");
        }

        private void UpdateLogEntries()
        {
            float currentTime = Time.time;
            var entriesToRemove = new List<LogEntry>();

            foreach (var entry in activeEntries)
            {
                if (entry.gameObject == null)
                {
                    entriesToRemove.Add(entry);
                    continue;
                }

                float age = currentTime - entry.spawnTime;

                // Start fading if past lifetime
                if (age >= messageLifetime && !entry.isFading)
                {
                    entry.isFading = true;
                }

                // Fade out
                if (entry.isFading)
                {
                    float fadeProgress = (age - messageLifetime) / fadeDuration;
                    if (fadeProgress >= 1f)
                    {
                        entriesToRemove.Add(entry);
                    }
                    else
                    {
                        entry.canvasGroup.alpha = 1f - fadeProgress;
                    }
                }
            }

            // Remove expired entries
            foreach (var entry in entriesToRemove)
            {
                RemoveEntry(entry);
            }
        }

        private void RemoveOldestEntry()
        {
            if (activeEntries.Count > 0)
            {
                RemoveEntry(activeEntries[0]);
            }
        }

        private void RemoveEntry(LogEntry entry)
        {
            activeEntries.Remove(entry);
            if (entry.gameObject != null)
            {
                Destroy(entry.gameObject);
            }
        }

        /// <summary>
        /// Clears all log entries
        /// </summary>
        public void Clear()
        {
            foreach (var entry in activeEntries)
            {
                if (entry.gameObject != null)
                    Destroy(entry.gameObject);
            }
            activeEntries.Clear();
        }
    }
}

