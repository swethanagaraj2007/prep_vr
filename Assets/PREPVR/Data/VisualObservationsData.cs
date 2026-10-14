using System;
using System.Collections.Generic;

[Serializable]
public class VisualObservationEntry
{
    public float timestampSeconds;
    public string category; // "Eye Contact", "Posture", "Body Movement", "General Demeanor"
    public string observation; // Observable factual statement only

    public VisualObservationEntry() { }

    public VisualObservationEntry(float timestampSeconds, string category, string observation)
    {
        this.timestampSeconds = timestampSeconds;
        this.category = category;
        this.observation = observation;
    }

    public string FormatTimestamp()
    {
        int minutes = (int)(timestampSeconds / 60f);
        int seconds = (int)(timestampSeconds % 60f);
        return $"{minutes:D2}:{seconds:D2}";
    }
}

[Serializable]
public class VisualObservationsData
{
    public List<VisualObservationEntry> entries = new List<VisualObservationEntry>();

    public string eyeContactSummary = "Candidate maintained forward gaze towards the interviewer table for the majority of responses.";
    public string postureSummary = "Posture remained upright and centered in the participant seat throughout the session.";
    public string engagementSummary = "Candidate demonstrated consistent head tracking towards the active speaker during questions.";

    public void AddObservation(float timestampSeconds, string category, string observation)
    {
        if (string.IsNullOrWhiteSpace(observation)) return;
        entries.Add(new VisualObservationEntry(timestampSeconds, category, observation.Trim()));
    }

    public void Clear()
    {
        entries.Clear();
    }
}
