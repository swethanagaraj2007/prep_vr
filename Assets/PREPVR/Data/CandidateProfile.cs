using System;
using System.Collections.Generic;

[Serializable]
public class CandidateProfile
{
    public string name;
    public string rollNumber;
    public string education;

    public List<string> skills = new List<string>();
    public List<string> projects = new List<string>();
    public List<string> experience = new List<string>();
    public List<string> certifications = new List<string>();
    public List<string> achievements = new List<string>();
}