"""
PREP VR — Vector PDF Placement Feedback Report Generator
Generates a multi-page, evidence-based placement assessment report using ReportLab.
Evaluates candidates across overall performance, rubric scoring, speech analysis,
question-by-question dialogue analysis, resume alignment, and actionable practice plan.
"""

import sys
import os
import json
from datetime import datetime
from reportlab.lib.pagesizes import letter
from reportlab.lib import colors
from reportlab.lib.styles import getSampleStyleSheet, ParagraphStyle
from reportlab.lib.units import inch
from reportlab.platypus import (
    SimpleDocTemplate,
    Paragraph,
    Spacer,
    Table,
    TableStyle,
    HRFlowable,
    KeepTogether,
)
from reportlab.pdfgen import canvas


class NumberedCanvas(canvas.Canvas):
    """Adds running headers and footers with dynamic page numbering."""

    def __init__(self, *args, **kwargs):
        super().__init__(*args, **kwargs)
        self._saved_page_states = []

    def showPage(self):
        self._saved_page_states.append(dict(self.__dict__))
        self._startPage()

    def save(self):
        num_pages = len(self._saved_page_states)
        for state in self._saved_page_states:
            self.__dict__.update(state)
            self.draw_header_footer(num_pages)
            super().showPage()
        super().save()

    def draw_header_footer(self, page_count):
        self.saveState()
        self.setFont("Helvetica-Bold", 8)
        self.setFillColor(colors.HexColor("#64748B"))

        # Top Header (pages 2+)
        if self._pageNumber > 1:
            self.drawString(
                54, 750, "PREP VR — AI Placement Interview & Performance Assessment"
            )
            self.setStrokeColor(colors.HexColor("#CBD5E1"))
            self.setLineWidth(0.5)
            self.line(54, 744, 558, 744)

        # Footer (all pages)
        self.setStrokeColor(colors.HexColor("#CBD5E1"))
        self.setLineWidth(0.5)
        self.line(54, 45, 558, 45)

        self.setFont("Helvetica", 8)
        self.drawString(
            54, 32, "Confidential Candidate Placement Performance Report — Grounded Practice Assessment"
        )
        self.drawRightString(
            558, 32, f"Page {self._pageNumber} of {page_count}"
        )
        self.restoreState()


def get_rating_color(rating_str):
    rating = (rating_str or "").strip().lower()
    if "exemplary" in rating or "excellent" in rating:
        return colors.HexColor("#059669")  # Emerald green
    elif "proficient" in rating or "strong" in rating:
        return colors.HexColor("#0284C7")  # Sky blue
    elif "developing" in rating or "moderate" in rating:
        return colors.HexColor("#D97706")  # Amber
    elif "insufficient" in rating:
        return colors.HexColor("#64748B")  # Slate gray
    else:
        return colors.HexColor("#DC2626")  # Rose red


def build_feedback_pdf(data, output_pdf_path):
    os.makedirs(os.path.dirname(os.path.abspath(output_pdf_path)), exist_ok=True)

    doc = SimpleDocTemplate(
        output_pdf_path,
        pagesize=letter,
        leftMargin=54,
        rightMargin=54,
        topMargin=54,
        bottomMargin=54,
    )

    styles = getSampleStyleSheet()

    # Custom styles
    header_title_style = ParagraphStyle(
        "HeaderTitle",
        parent=styles["Normal"],
        fontName="Helvetica-Bold",
        fontSize=18,
        leading=22,
        textColor=colors.HexColor("#0F172A"),
    )

    header_sub_style = ParagraphStyle(
        "HeaderSub",
        parent=styles["Normal"],
        fontName="Helvetica-Bold",
        fontSize=9.5,
        leading=13,
        textColor=colors.HexColor("#0284C7"),
    )

    meta_label_style = ParagraphStyle(
        "MetaLabel",
        parent=styles["Normal"],
        fontName="Helvetica-Bold",
        fontSize=8.5,
        leading=11,
        textColor=colors.HexColor("#475569"),
    )

    meta_val_style = ParagraphStyle(
        "MetaVal",
        parent=styles["Normal"],
        fontName="Helvetica",
        fontSize=8.5,
        leading=11,
        textColor=colors.HexColor("#0F172A"),
    )

    section_heading = ParagraphStyle(
        "SectionHeading",
        parent=styles["Normal"],
        fontName="Helvetica-Bold",
        fontSize=12,
        leading=16,
        textColor=colors.HexColor("#0F172A"),
    )

    body_style = ParagraphStyle(
        "ReportBody",
        parent=styles["Normal"],
        fontName="Helvetica",
        fontSize=9,
        leading=13,
        textColor=colors.HexColor("#334155"),
    )

    category_title = ParagraphStyle(
        "CatTitle",
        parent=styles["Normal"],
        fontName="Helvetica-Bold",
        fontSize=9,
        leading=12,
        textColor=colors.HexColor("#0F172A"),
    )

    rating_badge = ParagraphStyle(
        "RatingBadge",
        parent=styles["Normal"],
        fontName="Helvetica-Bold",
        fontSize=8.5,
        leading=11,
        alignment=2,  # Right-aligned
    )

    bullet_style = ParagraphStyle(
        "BulletItem",
        parent=styles["Normal"],
        fontName="Helvetica",
        fontSize=8.5,
        leading=12,
        textColor=colors.HexColor("#1E293B"),
        leftIndent=10,
    )

    disclaimer_style = ParagraphStyle(
        "DisclaimerStyle",
        parent=styles["Normal"],
        fontName="Helvetica-Oblique",
        fontSize=8,
        leading=11,
        textColor=colors.HexColor("#64748B"),
    )

    story = []

    # 1. Title Banner
    story.append(
        Paragraph("PREP VR — AI PLACEMENT INTERVIEW REPORT", header_sub_style)
    )
    story.append(Spacer(1, 3))
    story.append(
        Paragraph(
            "Evidence-Based Candidate Performance Assessment", header_title_style
        )
    )
    story.append(Spacer(1, 8))
    story.append(
        HRFlowable(
            width="100%",
            thickness=2,
            color=colors.HexColor("#0284C7"),
            spaceBefore=0,
            spaceAfter=10,
        )
    )

    # 2. Metadata Box
    candidate_name = data.get("candidateName") or "Candidate"
    candidate_edu = data.get("candidateEducation") or "Engineering Student"
    interview_date = data.get("interviewDate") or datetime.now().strftime(
        "%B %d, %Y"
    )
    duration_str = data.get("interviewDurationFormatted") or "10:00"
    track_type = (
        data.get("interviewType") or "Placement Practice — Panel Mock Interview"
    )
    readiness = (
        data.get("overallReadinessLevel") or "Placement Ready — Proficient"
    )

    meta_table_data = [
        [
            Paragraph("<b>Candidate:</b>", meta_label_style),
            Paragraph(candidate_name, meta_val_style),
            Paragraph("<b>Interview Track:</b>", meta_label_style),
            Paragraph(track_type, meta_val_style),
        ],
        [
            Paragraph("<b>Education:</b>", meta_label_style),
            Paragraph(candidate_edu, meta_val_style),
            Paragraph("<b>Session Duration:</b>", meta_label_style),
            Paragraph(f"{duration_str} (Full Session)", meta_val_style),
        ],
        [
            Paragraph("<b>Evaluation Date:</b>", meta_label_style),
            Paragraph(interview_date, meta_val_style),
            Paragraph("<b>Readiness Level:</b>", meta_label_style),
            Paragraph(f"<b>{readiness}</b>", meta_val_style),
        ],
    ]

    meta_table = Table(
        meta_table_data, colWidths=[80, 172, 100, 152], rowHeights=18
    )
    meta_table.setStyle(
        TableStyle(
            [
                ("BACKGROUND", (0, 0), (-1, -1), colors.HexColor("#F8FAFC")),
                ("BOX", (0, 0), (-1, -1), 1, colors.HexColor("#E2E8F0")),
                ("INNERGRID", (0, 0), (-1, -1), 0.5, colors.HexColor("#EDF2F7")),
                ("VALIGN", (0, 0), (-1, -1), "MIDDLE"),
                ("TOPPADDING", (0, 0), (-1, -1), 2),
                ("BOTTOMPADDING", (0, 0), (-1, -1), 2),
            ]
        )
    )
    story.append(meta_table)
    story.append(Spacer(1, 12))

    # 3. Overall Performance & Scoring Rubric
    story.append(Paragraph("1. Overall Performance & Scoring Rubric", section_heading))
    story.append(Spacer(1, 4))

    summary_text = data.get("overallSummary") or (
        f"{candidate_name} completed the {duration_str} mock interview session."
    )
    story.append(Paragraph(summary_text, body_style))
    story.append(Spacer(1, 6))

    rubric = data.get("rubric", {})
    overall_score = rubric.get("overallScore", 0)
    score_scale = rubric.get("scoringScale", "0-100 (Exemplary: 85-100, Proficient: 70-84, Developing: 50-69, Needs Focus: <50)")

    rubric_rows = [
        [
            Paragraph("<b>Scoring Criterion</b>", meta_label_style),
            Paragraph("<b>Weight</b>", meta_label_style),
            Paragraph("<b>Score (/100)</b>", meta_label_style),
            Paragraph("<b>Assessment Rubric Description</b>", meta_label_style),
        ],
        [
            Paragraph("<b>Question Relevance</b>", category_title),
            Paragraph("20%", body_style),
            Paragraph(f"<b>{rubric.get('relevanceScore', 0)}</b>", body_style),
            Paragraph("Direct alignment between interviewer inquiries and spoken candidate answers.", body_style),
        ],
        [
            Paragraph("<b>Technical Correctness</b>", category_title),
            Paragraph("20%", body_style),
            Paragraph(f"<b>{rubric.get('technicalCorrectnessScore', 0)}</b>", body_style),
            Paragraph("Accuracy of tools, architecture components, and technical vocabulary.", body_style),
        ],
        [
            Paragraph("<b>Communication Effectiveness</b>", category_title),
            Paragraph("15%", body_style),
            Paragraph(f"<b>{rubric.get('communicationScore', 0)}</b>", body_style),
            Paragraph("Fluency, articulation clarity, and control of conversational hesitation fillers.", body_style),
        ],
        [
            Paragraph("<b>Answer Structure (STAR)</b>", category_title),
            Paragraph("15%", body_style),
            Paragraph(f"<b>{rubric.get('structureClarityScore', 0)}</b>", body_style),
            Paragraph("Logical framing: Context/Situation -> Task -> Action -> Measurable Result.", body_style),
        ],
        [
            Paragraph("<b>Specificity & Evidence</b>", category_title),
            Paragraph("15%", body_style),
            Paragraph(f"<b>{rubric.get('specificityScore', 0)}</b>", body_style),
            Paragraph("Grounding in real project implementation, metrics, and technical decisions.", body_style),
        ],
        [
            Paragraph("<b>Behavioural Quality</b>", category_title),
            Paragraph("10%", body_style),
            Paragraph(f"<b>{rubric.get('behaviouralQualityScore', 0)}</b>", body_style),
            Paragraph("Personal ownership, teamwork dynamics, and problem-solving initiative.", body_style),
        ],
        [
            Paragraph("<b>Conciseness & Precision</b>", category_title),
            Paragraph("5%", body_style),
            Paragraph(f"<b>{rubric.get('concisenessScore', 0)}</b>", body_style),
            Paragraph("Efficient delivery avoiding unnecessary repetition or circular phrasing.", body_style),
        ],
        [
            Paragraph("<b>COMPOSITE OVERALL SCORE</b>", category_title),
            Paragraph("<b>100%</b>", category_title),
            Paragraph(f"<b><font size=11 color='#0284C7'>{overall_score} / 100</font></b>", category_title),
            Paragraph(f"<b>Scale:</b> {score_scale}", body_style),
        ],
    ]

    rubric_table = Table(rubric_rows, colWidths=[140, 50, 75, 239])
    rubric_table.setStyle(
        TableStyle(
            [
                ("BACKGROUND", (0, 0), (-1, 0), colors.HexColor("#0F172A")),
                ("TEXTCOLOR", (0, 0), (-1, 0), colors.white),
                ("BOX", (0, 0), (-1, -1), 1, colors.HexColor("#CBD5E1")),
                ("INNERGRID", (0, 0), (-1, -1), 0.5, colors.HexColor("#E2E8F0")),
                ("BACKGROUND", (0, -1), (-1, -1), colors.HexColor("#F1F5F9")),
                ("VALIGN", (0, 0), (-1, -1), "MIDDLE"),
                ("TOPPADDING", (0, 0), (-1, -1), 3),
                ("BOTTOMPADDING", (0, 0), (-1, -1), 3),
            ]
        )
    )
    story.append(rubric_table)
    story.append(Spacer(1, 12))

    # 4. Speech-Based Analysis
    story.append(Paragraph("2. Speech-Based Analysis", section_heading))
    story.append(Spacer(1, 4))

    speech = data.get("speechMetrics", {})
    words_count = speech.get("totalWordsSpoken", 0)
    wpm_val = speech.get("speakingPaceWpm", 0.0)
    pace_text = speech.get("paceAssessment", "Not Assessed")
    filler_count = speech.get("fillerWordCount", 0)
    fillers_list = speech.get("fillerWordsDetected", [])
    fillers_str = ", ".join(fillers_list) if fillers_list else "None detected"
    confidence_text = speech.get("verbalConfidenceIndicators", "Spoken articulation was steady.")

    speech_table_data = [
        [
            Paragraph("<b>Metric</b>", meta_label_style),
            Paragraph("<b>Measured Value</b>", meta_label_style),
            Paragraph("<b>Evaluation & Qualitative Assessment</b>", meta_label_style),
        ],
        [
            Paragraph("<b>Speaking Pace (WPM)</b>", category_title),
            Paragraph(f"~{wpm_val:.0f} WPM", body_style),
            Paragraph(pace_text, body_style),
        ],
        [
            Paragraph("<b>Total Spoken Words</b>", category_title),
            Paragraph(f"{words_count} words", body_style),
            Paragraph("Total volume of candidate speech captured and transcribed across panel turns.", body_style),
        ],
        [
            Paragraph("<b>Filler Word Frequency</b>", category_title),
            Paragraph(f"{filler_count} occurrences", body_style),
            Paragraph(f"Detected fillers: <i>{fillers_str}</i>. Replacing with intentional 1s pauses enhances composure.", body_style),
        ],
        [
            Paragraph("<b>Verbal Confidence</b>", category_title),
            Paragraph("Observable delivery", body_style),
            Paragraph(confidence_text, body_style),
        ],
    ]

    speech_table = Table(speech_table_data, colWidths=[130, 95, 279])
    speech_table.setStyle(
        TableStyle(
            [
                ("BACKGROUND", (0, 0), (-1, 0), colors.HexColor("#F8FAFC")),
                ("BOX", (0, 0), (-1, -1), 1, colors.HexColor("#CBD5E1")),
                ("INNERGRID", (0, 0), (-1, -1), 0.5, colors.HexColor("#E2E8F0")),
                ("VALIGN", (0, 0), (-1, -1), "TOP"),
                ("TOPPADDING", (0, 0), (-1, -1), 3),
                ("BOTTOMPADDING", (0, 0), (-1, -1), 3),
            ]
        )
    )
    story.append(speech_table)
    story.append(Spacer(1, 4))

    disclaimer = speech.get("sensorLimitationsDisclaimer") or (
        "Hardware Limitation Note: Pitch, emotion, and corneal eye-tracking are not captured by current hardware sensors. Gaze direction reflects VR headset forward orientation."
    )
    story.append(Paragraph(f"ℹ {disclaimer}", disclaimer_style))
    story.append(Spacer(1, 12))

    # 5. Question-by-Question Feedback
    q_feedback = data.get("questionFeedbackList", [])
    if q_feedback:
        story.append(Paragraph("3. Question-by-Question Performance Breakdown", section_heading))
        story.append(Spacer(1, 4))

        for q_item in q_feedback:
            q_num = q_item.get("questionIndex", 1)
            q_role = q_item.get("stageRole", "Interviewer")
            q_text = q_item.get("question", "")
            a_text = q_item.get("candidateAnswer", "")
            well_text = q_item.get("whatWentWell", "")
            improve_text = q_item.get("whatCouldBeImproved", "")
            structure_text = q_item.get("suggestedStrongerStructure", "")

            q_card_data = [
                [
                    Paragraph(f"<b>Question #{q_num} [{q_role}]:</b> {q_text}", category_title),
                ],
                [
                    Paragraph(f"<b>Candidate Answer:</b> \"{a_text}\"", body_style),
                ],
                [
                    Paragraph(f"✔ <b>What was done well:</b> {well_text}", bullet_style),
                ],
                [
                    Paragraph(f"⚠ <b>What could be improved:</b> {improve_text}", bullet_style),
                ],
                [
                    Paragraph(f"★ <b>Suggested Structure:</b> {structure_text}", bullet_style),
                ],
            ]
            q_table = Table(q_card_data, colWidths=[504])
            q_table.setStyle(
                TableStyle(
                    [
                        ("BACKGROUND", (0, 0), (-1, 0), colors.HexColor("#F1F5F9")),
                        ("BOX", (0, 0), (-1, -1), 1, colors.HexColor("#CBD5E1")),
                        ("INNERGRID", (0, 0), (-1, -1), 0.5, colors.HexColor("#E2E8F0")),
                        ("TOPPADDING", (0, 0), (-1, -1), 3),
                        ("BOTTOMPADDING", (0, 0), (-1, -1), 3),
                    ]
                )
            )
            story.append(q_table)
            story.append(Spacer(1, 6))

        story.append(Spacer(1, 8))

    # 6. Resume Alignment Report
    story.append(Paragraph("4. Resume Alignment Analysis", section_heading))
    story.append(Spacer(1, 4))

    resume_rep = data.get("resumeAlignmentReport", {})
    demo_skills = resume_rep.get("demonstratedSkills", [])
    undemo_skills = resume_rep.get("undemonstratedSkills", [])
    align_summary = resume_rep.get("alignmentSummary", "Resume credentials aligned with interview discussion.")

    demo_str = ", ".join(demo_skills) if demo_skills else "None explicitly cited"
    undemo_str = ", ".join(undemo_skills[:8]) if undemo_skills else "All core skills were touched"

    resume_table_data = [
        [
            Paragraph("<b>Resume Element</b>", meta_label_style),
            Paragraph("<b>Identified Evidence & Status</b>", meta_label_style),
        ],
        [
            Paragraph("<b>Demonstrated Competencies</b>", category_title),
            Paragraph(f"<font color='#059669'><b>✔ Spoken in Session:</b></font> {demo_str}", body_style),
        ],
        [
            Paragraph("<b>Undemonstrated Resume Skills</b>", category_title),
            Paragraph(f"<font color='#64748B'><b>Unaddressed (Scope limit):</b></font> {undemo_str}", body_style),
        ],
        [
            Paragraph("<b>Alignment Assessment</b>", category_title),
            Paragraph(align_summary, body_style),
        ],
    ]
    resume_table = Table(resume_table_data, colWidths=[150, 354])
    resume_table.setStyle(
        TableStyle(
            [
                ("BACKGROUND", (0, 0), (-1, 0), colors.HexColor("#F8FAFC")),
                ("BOX", (0, 0), (-1, -1), 1, colors.HexColor("#CBD5E1")),
                ("INNERGRID", (0, 0), (-1, -1), 0.5, colors.HexColor("#E2E8F0")),
                ("VALIGN", (0, 0), (-1, -1), "TOP"),
                ("TOPPADDING", (0, 0), (-1, -1), 3),
                ("BOTTOMPADDING", (0, 0), (-1, -1), 3),
            ]
        )
    )
    story.append(resume_table)
    story.append(Spacer(1, 12))

    # 7. Action Plan
    story.append(Paragraph("5. Prioritized Action Plan & Practice Questions", section_heading))
    story.append(Spacer(1, 4))

    action_plan = data.get("prioritizedActionPlan", [])
    if action_plan:
        for plan_item in action_plan:
            p_num = plan_item.get("priority", 1)
            p_title = plan_item.get("title", "")
            p_rec = plan_item.get("recommendation", "")
            p_q = plan_item.get("practiceQuestion", "")

            plan_card_data = [
                [
                    Paragraph(f"<b>Priority {p_num}: {p_title}</b>", category_title),
                ],
                [
                    Paragraph(f"<b>Action Guidance:</b> {p_rec}", body_style),
                ],
                [
                    Paragraph(f"<b>Recommended Practice Question:</b> <i>\"{p_q}\"</i>", bullet_style),
                ],
            ]
            plan_table = Table(plan_card_data, colWidths=[504])
            plan_table.setStyle(
                TableStyle(
                    [
                        ("BACKGROUND", (0, 0), (-1, 0), colors.HexColor("#ECFDF5")),
                        ("BOX", (0, 0), (-1, -1), 1, colors.HexColor("#CBD5E1")),
                        ("INNERGRID", (0, 0), (-1, -1), 0.5, colors.HexColor("#E2E8F0")),
                        ("TOPPADDING", (0, 0), (-1, -1), 3),
                        ("BOTTOMPADDING", (0, 0), (-1, -1), 3),
                    ]
                )
            )
            story.append(plan_table)
            story.append(Spacer(1, 5))
    else:
        recs = data.get("actionableRecommendations") or [
            "1. Quantify Resume Achievements: Cite specific metrics (latency, scale, throughput).",
            "2. Drill 2-Minute STAR Pitch: Structure answers sequentially from Context to Measurable Result.",
            "3. Practice Intentional Pausing: Replace filler words with silent 1-second pauses.",
        ]
        for r in recs:
            story.append(Paragraph(r, bullet_style))
            story.append(Spacer(1, 3))

    doc.build(story, canvasmaker=NumberedCanvas)
    print(f"Generated Vector PDF report at: {output_pdf_path}")
    return output_pdf_path


def main():
    if len(sys.argv) < 3:
        print(
            "Usage: python generate_feedback_pdf.py <input_json_path> <output_pdf_path>"
        )
        sys.exit(1)

    json_path = sys.argv[1]
    pdf_path = sys.argv[2]

    if not os.path.exists(json_path):
        print(f"Error: Input JSON file does not exist: {json_path}")
        sys.exit(1)

    with open(json_path, "r", encoding="utf-8") as f:
        data = json.load(f)

    build_feedback_pdf(data, pdf_path)


if __name__ == "__main__":
    main()
