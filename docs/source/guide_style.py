"""Page layout, typography and small building blocks for the engineer guide."""

from reportlab.lib import colors
from reportlab.lib.enums import TA_CENTER, TA_JUSTIFY, TA_LEFT
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle
from reportlab.lib.units import cm
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.platypus import (BaseDocTemplate, CondPageBreak, Frame, Image, KeepTogether,
                                ListFlowable, ListItem, NextPageTemplate, PageBreak, PageTemplate,
                                Paragraph, Preformatted, Spacer, Table, TableStyle)
from reportlab.platypus.tableofcontents import TableOfContents

import os

import matplotlib

# The DejaVu fonts bundled with matplotlib: available wherever the build's dependencies are,
# on Linux, macOS and Windows alike.
FONT_DIR = os.path.join(os.path.dirname(matplotlib.__file__), "mpl-data", "fonts", "ttf") + os.sep
pdfmetrics.registerFont(TTFont("Serif", FONT_DIR + "DejaVuSerif.ttf"))
pdfmetrics.registerFont(TTFont("Serif-Bold", FONT_DIR + "DejaVuSerif-Bold.ttf"))
pdfmetrics.registerFont(TTFont("Serif-Italic", FONT_DIR + "DejaVuSerif-Italic.ttf"))
pdfmetrics.registerFont(TTFont("Serif-BoldItalic", FONT_DIR + "DejaVuSerif-BoldItalic.ttf"))
pdfmetrics.registerFont(TTFont("Sans", FONT_DIR + "DejaVuSans.ttf"))
pdfmetrics.registerFont(TTFont("Sans-Bold", FONT_DIR + "DejaVuSans-Bold.ttf"))
pdfmetrics.registerFont(TTFont("Mono", FONT_DIR + "DejaVuSansMono.ttf"))
pdfmetrics.registerFont(TTFont("Sans-Italic", FONT_DIR + "DejaVuSans-Oblique.ttf"))
pdfmetrics.registerFontFamily("Serif", normal="Serif", bold="Serif-Bold",
                              italic="Serif-Italic", boldItalic="Serif-BoldItalic")
pdfmetrics.registerFontFamily("Sans", normal="Sans", bold="Sans-Bold", italic="Sans-Italic", boldItalic="Sans-Bold")

# Palette shared with the figures.
INK = colors.HexColor("#1f2933")
MUTED = colors.HexColor("#52606d")
ACCENT = colors.HexColor("#1d4e89")      # deterministic / islands
STOCHASTIC = colors.HexColor("#c05621")  # stochastic core
SAFE = colors.HexColor("#2f855a")
ALERT = colors.HexColor("#b83280")
RULE = colors.HexColor("#cbd2d9")
PANEL = colors.HexColor("#f0f4f8")

PAGE_W, PAGE_H = A4
MARGIN_X = 2.3 * cm
MARGIN_TOP = 2.4 * cm
MARGIN_BOTTOM = 2.2 * cm
TEXT_W = PAGE_W - 2 * MARGIN_X

GUIDE_TITLE = "Deterministic Islands in Practice"
GUIDE_SUBTITLE = "An Engineer-to-Engineer Guide to the NEXUS-1 Proof of Concept"

base = ParagraphStyle("Body", fontName="Serif", fontSize=10, leading=14.6, textColor=INK,
                      alignment=TA_JUSTIFY, spaceAfter=6.5)
STYLES = {
    "body": base,
    "lead": ParagraphStyle("Lead", parent=base, fontSize=11.2, leading=16.2, textColor=MUTED,
                           alignment=TA_LEFT, spaceAfter=10),
    "h1": ParagraphStyle("H1", fontName="Sans-Bold", fontSize=21, leading=26, textColor=ACCENT,
                         spaceBefore=0, spaceAfter=14),
    "part": ParagraphStyle("Part", fontName="Sans-Bold", fontSize=30, leading=36, textColor=ACCENT,
                           alignment=TA_LEFT, spaceAfter=18),
    "h2": ParagraphStyle("H2", fontName="Sans-Bold", fontSize=13.2, leading=17, textColor=INK,
                         spaceBefore=12, spaceAfter=6),
    "h3": ParagraphStyle("H3", fontName="Sans-Bold", fontSize=10.8, leading=14, textColor=ACCENT,
                         spaceBefore=8, spaceAfter=3),
    "caption": ParagraphStyle("Caption", fontName="Serif-Italic", fontSize=8.8, leading=12,
                              textColor=MUTED, alignment=TA_CENTER, spaceBefore=3, spaceAfter=12),
    "code": ParagraphStyle("Code", fontName="Mono", fontSize=8.2, leading=11, textColor=INK),
    "cell": ParagraphStyle("Cell", fontName="Serif", fontSize=8.8, leading=11.6, textColor=INK),
    "cellhead": ParagraphStyle("CellHead", fontName="Sans-Bold", fontSize=8.6, leading=11,
                               textColor=colors.white),
    "callout": ParagraphStyle("Callout", parent=base, fontSize=9.5, leading=13.6, spaceAfter=3),
    "callout_title": ParagraphStyle("CalloutTitle", fontName="Sans-Bold", fontSize=9.4, leading=12,
                                    spaceAfter=3),
    "toc1": ParagraphStyle("TOC1", fontName="Sans-Bold", fontSize=10.5, leading=15, leftIndent=0,
                           textColor=INK, spaceBefore=5),
    "toc2": ParagraphStyle("TOC2", fontName="Serif", fontSize=9.6, leading=13, leftIndent=16, textColor=INK),
    "bullet": ParagraphStyle("Bullet", parent=base, spaceAfter=2.5),
}


class GuideDoc(BaseDocTemplate):
    """Two page templates (cover, content) and TOC notifications for h1/h2."""

    def __init__(self, filename, **kw):
        super().__init__(filename, pagesize=A4, leftMargin=MARGIN_X, rightMargin=MARGIN_X,
                         topMargin=MARGIN_TOP, bottomMargin=MARGIN_BOTTOM, **kw)
        frame = Frame(MARGIN_X, MARGIN_BOTTOM, TEXT_W, PAGE_H - MARGIN_TOP - MARGIN_BOTTOM, id="body")
        cover_frame = Frame(0, 0, PAGE_W, PAGE_H, leftPadding=0, rightPadding=0, topPadding=0,
                            bottomPadding=0, id="cover")
        self.addPageTemplates([
            PageTemplate(id="cover", frames=[cover_frame], onPage=draw_cover),
            PageTemplate(id="content", frames=[frame], onPage=draw_page_furniture),
        ])
        self._heading_counter = 0

    def beforeDocument(self):
        self._heading_counter = 0

    def afterFlowable(self, flowable):
        if isinstance(flowable, Paragraph) and hasattr(flowable, "toc_level"):
            text = flowable.toc_text
            key = f"h{self._heading_counter}"
            self._heading_counter += 1
            self.canv.bookmarkPage(key)
            self.canv.addOutlineEntry(text, key, level=flowable.toc_level, closed=flowable.toc_level > 0)
            self.notify("TOCEntry", (flowable.toc_level, text, self.page, key))


def draw_page_furniture(canvas, doc):
    canvas.saveState()
    canvas.setStrokeColor(RULE)
    canvas.setLineWidth(0.6)
    canvas.line(MARGIN_X, PAGE_H - 1.55 * cm, PAGE_W - MARGIN_X, PAGE_H - 1.55 * cm)
    canvas.setFont("Sans", 7.8)
    canvas.setFillColor(MUTED)
    canvas.drawString(MARGIN_X, PAGE_H - 1.35 * cm, GUIDE_TITLE.upper())
    canvas.drawRightString(PAGE_W - MARGIN_X, PAGE_H - 1.35 * cm, "NEXUS-1 · DeterministicIsland POC")
    canvas.line(MARGIN_X, 1.45 * cm, PAGE_W - MARGIN_X, 1.45 * cm)
    canvas.drawString(MARGIN_X, 1.0 * cm, "Engineer-to-Engineer Guide")
    canvas.setFont("Sans-Bold", 8.5)
    canvas.drawRightString(PAGE_W - MARGIN_X, 1.0 * cm, str(doc.page))
    canvas.restoreState()


def draw_cover(canvas, doc):
    canvas.saveState()
    canvas.setFillColor(colors.HexColor("#0f2a47"))
    canvas.rect(0, 0, PAGE_W, PAGE_H, stroke=0, fill=1)
    # A field of stochastic dots with calm deterministic "islands".
    import random
    rnd = random.Random(12)  # fixed seed: the cover itself is a frozen snapshot
    for _ in range(900):
        x, y = rnd.uniform(0, PAGE_W), rnd.uniform(3 * cm, PAGE_H * 0.62)
        canvas.setFillColor(colors.Color(0.95, 0.55, 0.25, alpha=rnd.uniform(0.08, 0.35)))
        canvas.circle(x, y, rnd.uniform(0.6, 2.2), stroke=0, fill=1)
    for cx, cy, r in [(4.2 * cm, 9.5 * cm, 1.9 * cm), (11.5 * cm, 6.3 * cm, 2.6 * cm), (16.4 * cm, 13.2 * cm, 1.6 * cm)]:
        canvas.setFillColor(colors.HexColor("#0f2a47"))
        canvas.circle(cx, cy, r + 0.35 * cm, stroke=0, fill=1)
        canvas.setFillColor(colors.HexColor("#3b82c4"))
        canvas.setStrokeColor(colors.HexColor("#9cc3ec"))
        canvas.setLineWidth(1.2)
        canvas.circle(cx, cy, r, stroke=1, fill=1)
        canvas.setStrokeColor(colors.HexColor("#d6e6f7"))
        canvas.setLineWidth(0.6)
        for k in range(1, 4):
            canvas.circle(cx, cy, r * k / 4, stroke=1, fill=0)
    canvas.setFillColor(colors.white)
    canvas.setFont("Sans", 10.5)
    canvas.drawString(2.3 * cm, PAGE_H - 3.0 * cm, "NEXUS-1 SERIES  ·  COMPANION GUIDE")
    canvas.setFont("Sans-Bold", 34)
    canvas.drawString(2.3 * cm, PAGE_H - 5.3 * cm, "Deterministic Islands")
    canvas.drawString(2.3 * cm, PAGE_H - 6.75 * cm, "in Practice")
    canvas.setFont("Sans", 14)
    canvas.setFillColor(colors.HexColor("#c9ddf2"))
    canvas.drawString(2.3 * cm, PAGE_H - 8.1 * cm, "An Engineer-to-Engineer Guide to the")
    canvas.drawString(2.3 * cm, PAGE_H - 8.85 * cm, "NEXUS-1 DeterministicIsland Proof of Concept")
    canvas.setFont("Serif-Italic", 10.5)
    canvas.setFillColor(colors.white)
    canvas.drawString(2.3 * cm, PAGE_H - 10.4 * cm,
                      "Probabilistic core, deterministic shell: how a small C# system keeps a")
    canvas.drawString(2.3 * cm, PAGE_H - 11.0 * cm,
                      "neural network inside boundaries it cannot learn its way around.")
    canvas.setFont("Sans", 9)
    canvas.setFillColor(colors.HexColor("#c9ddf2"))
    canvas.drawString(2.3 * cm, 2.0 * cm,
                      "Based on G. Agathangelidis, From Stochastic Chaos to Deterministic Certainty (NEXUS-1, Vol. III)")
    canvas.drawString(2.3 * cm, 1.45 * cm, "Repository: github.com/gregory82gr/DeterministicIsland")
    canvas.restoreState()


# ---------------------------------------------------------------------------
# Story builder
# ---------------------------------------------------------------------------

class Story(list):
    """A list of flowables with helpers that keep the chapter files readable."""

    def part(self, number, title, intro):
        self.append(PageBreak())
        self.append(Spacer(1, 4.5 * cm))
        label = f"<font size=13 color='#52606d'>PART {number}</font><br/>" if number else ""
        p = Paragraph(f"{label}{title}", STYLES["part"])
        p.toc_level, p.toc_text = 0, f"Part {number} — {title}" if number else title
        self.append(p)
        self.append(Paragraph(intro, STYLES["lead"]))
        self.append(PageBreak())

    def chapter(self, number, title, lead=None):
        self.append(CondPageBreak(8 * cm))
        p = Paragraph(f"<font color='#52606d'>{number}</font>&nbsp;&nbsp;{title}" if number else title, STYLES["h1"])
        # Unnumbered front-matter chapters sit at the top level of the outline.
        p.toc_level, p.toc_text = (1, f"{number}  {title}") if number else (0, title)
        self.append(p)
        if lead:
            self.append(Paragraph(lead, STYLES["lead"]))

    def section(self, title):
        self.append(CondPageBreak(3.2 * cm))
        self.append(Paragraph(title, STYLES["h2"]))

    def sub(self, title):
        self.append(CondPageBreak(2.4 * cm))
        self.append(Paragraph(title, STYLES["h3"]))

    def p(self, *texts):
        for text in texts:
            self.append(Paragraph(text, STYLES["body"]))

    def bullets(self, items, numbered=False):
        flow = ListFlowable(
            [ListItem(Paragraph(i, STYLES["bullet"]), leftIndent=14) for i in items],
            bulletType="1" if numbered else "bullet", start="1" if numbered else "•",
            leftIndent=14, bulletFontName="Sans", bulletFontSize=8.5, bulletColor=ACCENT)
        self.append(flow)
        self.append(Spacer(1, 4))

    CODE_WIDTH = 90  # characters of DejaVu Sans Mono 8.2 pt that fit the text column

    def code(self, text, truncate=False):
        lines = []
        for line in text.strip("\n").splitlines():
            if len(line) <= self.CODE_WIDTH:
                lines.append(line)
            elif truncate:
                lines.append(line[: self.CODE_WIDTH - 1] + "…")
            else:
                indent = " " * (len(line) - len(line.lstrip()) + 4)
                lines.append(line[: self.CODE_WIDTH])
                rest = line[self.CODE_WIDTH:]
                while rest:
                    room = self.CODE_WIDTH - len(indent)
                    lines.append(indent + rest[:room])
                    rest = rest[room:]
        pre = Preformatted("\n".join(lines), STYLES["code"])
        t = Table([[pre]], colWidths=[TEXT_W])
        t.setStyle(TableStyle([
            ("BACKGROUND", (0, 0), (-1, -1), colors.HexColor("#f5f7fa")),
            ("BOX", (0, 0), (-1, -1), 0.5, RULE),
            ("LEFTPADDING", (0, 0), (-1, -1), 9), ("RIGHTPADDING", (0, 0), (-1, -1), 9),
            ("TOPPADDING", (0, 0), (-1, -1), 7), ("BOTTOMPADDING", (0, 0), (-1, -1), 7),
        ]))
        self.append(t)
        self.append(Spacer(1, 8))

    def callout(self, kind, title, text):
        color = {"key": ACCENT, "warn": ALERT, "ours": SAFE, "try": STOCHASTIC}[kind]
        label = {"key": "KEY IDEA", "warn": "HONEST BOUNDARY", "ours": "OUR APPROACH", "try": "TRY IT"}[kind]
        body = [Paragraph(f"<font color='{color.hexval()}'>{label}</font>&nbsp;·&nbsp;{title}", STYLES["callout_title"])]
        for para in (text if isinstance(text, list) else [text]):
            body.append(Paragraph(para, STYLES["callout"]))
        t = Table([[body]], colWidths=[TEXT_W])
        t.setStyle(TableStyle([
            ("BACKGROUND", (0, 0), (-1, -1), PANEL),
            ("LINEBEFORE", (0, 0), (0, -1), 3, color),
            ("LEFTPADDING", (0, 0), (-1, -1), 11), ("RIGHTPADDING", (0, 0), (-1, -1), 10),
            ("TOPPADDING", (0, 0), (-1, -1), 8), ("BOTTOMPADDING", (0, 0), (-1, -1), 6),
        ]))
        self.append(Spacer(1, 3))
        self.append(t)
        self.append(Spacer(1, 9))

    def table(self, rows, widths, header=True, zebra=True):
        data = []
        for r, row in enumerate(rows):
            style = STYLES["cellhead"] if header and r == 0 else STYLES["cell"]
            data.append([c if not isinstance(c, str) else Paragraph(c, style) for c in row])
        t = Table(data, colWidths=[w * TEXT_W for w in widths], repeatRows=1 if header else 0)
        cmds = [
            ("VALIGN", (0, 0), (-1, -1), "TOP"),
            ("LINEBELOW", (0, 0), (-1, -1), 0.4, RULE),
            ("LEFTPADDING", (0, 0), (-1, -1), 5), ("RIGHTPADDING", (0, 0), (-1, -1), 5),
            ("TOPPADDING", (0, 0), (-1, -1), 4), ("BOTTOMPADDING", (0, 0), (-1, -1), 4),
        ]
        if header:
            cmds.append(("BACKGROUND", (0, 0), (-1, 0), ACCENT))
        if zebra:
            for r in range(1 if header else 0, len(rows)):
                if r % 2 == 0:
                    cmds.append(("BACKGROUND", (0, r), (-1, r), colors.HexColor("#f7f9fb")))
        t.setStyle(TableStyle(cmds))
        self.append(t)
        self.append(Spacer(1, 10))

    def figure(self, path, caption, width=1.0, heading=None):
        from reportlab.lib.utils import ImageReader
        iw, ih = ImageReader(path).getSize()
        w = TEXT_W * width
        h = w * ih / iw
        img = Image(path, width=w, height=h)
        parts = [img, Paragraph(caption, STYLES["caption"])]
        if heading:
            parts.insert(0, Paragraph(heading, STYLES["h2"]))
        self.append(KeepTogether(parts))

    def toc(self):
        toc = TableOfContents()
        toc.levelStyles = [STYLES["toc1"], STYLES["toc2"]]
        toc.dotsMinLevel = 1
        self.append(Paragraph("Contents", STYLES["h1"]))
        self.append(toc)


__all__ = ["Story", "GuideDoc", "STYLES", "NextPageTemplate", "PageBreak", "Spacer", "Paragraph",
           "TEXT_W", "cm", "ACCENT", "STOCHASTIC", "SAFE", "ALERT", "MUTED"]
