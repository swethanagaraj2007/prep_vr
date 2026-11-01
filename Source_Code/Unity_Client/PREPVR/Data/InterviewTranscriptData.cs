using System;
using System.Collections.Generic;

[Serializable]
public class TranscriptEntry
{
    public float timestampSeconds;
    public string speaker;
    public string text;
    public bool isQuestion;

    public TranscriptEntry() { }

    public TranscriptEntry(float timestampSeconds, string speaker, string text, bool isQuestion = false)
    {
        this.timestampSeconds = timestampSeconds;
        this.speaker = speaker;
        this.text = text;
        this.isQuestion = isQuestion;
    }

    public string FormatTimestamp()
    {
        int minutes = Mathf.FloorToInt(timestampSeconds / 60f);
        int seconds = Mathf.FloorToInt(timestampSeconds % 60f);
        return $"{minutes:D2}:{seconds:D2}";
    }

    private static class Mathf
    {
        public static int FloorToInt(float value) => (int)Math.Floor(value);
    }
}

[Serializable]
public class InterviewTranscriptData
{
    public List<TranscriptEntry> entries = new List<TranscriptEntry>();
    public List<InterviewResponse> qaResponses = new List<InterviewResponse>();

    public void AddEntry(float timestampSeconds, string speaker, string text, bool isQuestion = false)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        entries.Add(new TranscriptEntry(timestampSeconds, speaker, text.Trim(), isQuestion));
    }

    public void AddQAResponse(string question, string answer)
    {
        if (string.IsNullOrWhiteSpace(question) && string.IsNullOrWhiteSpace(answer)) return;

        qaResponses.Add(new InterviewResponse
        {
            questionNumber = qaResponses.Count + 1,
            question = question ?? string.Empty,
            answer = answer ?? string.Empty
        });
    }

    public int TotalResponses => qaResponses.Count;

    public int TotalQuestions
    {
        get
        {
            int count = 0;
            if (entries != null)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    if (entries[i] != null && entries[i].isQuestion) count++;
                }
            }
            return count > 0 ? count : qaResponses.Count;
        }
    }

    public void Clear()
    {
        entries.Clear();
        qaResponses.Clear();
    }
}
