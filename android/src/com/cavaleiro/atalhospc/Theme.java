package com.cavaleiro.atalhospc;

/** Os 10 temas. Cada um define o aparelho inteiro: mesa, chassi, luz da borda, teclas, telas e as cores dos pads de som. */
final class Theme {
    final int id;
    final String name;
    final String note;
    int deskTop, deskBottom;
    int chassisTop, chassisBottom, chassisEdge;
    int[] rim;
    float glow = 1f;
    int capTop, capBottom, skirt, socket, socketEdge;
    int lcd = 0xFF050506;
    float lcdTint = 0.30f;
    int gloss = 0x2EFFFFFF;
    int label = 0xFFFFFFFF, labelShade = 0xD8000000;
    int mark = 0x1EFFFFFF;
    int accent, text, muted;
    int bg, card, cardHigh;
    int insetTop, insetBottom, insetEdge;
    int buttonTop, buttonBottom, buttonEdge;
    int[] pads;

    private Theme(int id, String name, String note) {
        this.id = id;
        this.name = name;
        this.note = note;
    }

    private Theme desk(int top, int bottom) {
        deskTop = top;
        deskBottom = bottom;
        bg = bottom;
        return this;
    }

    private Theme chassis(int top, int bottom, int edge) {
        chassisTop = top;
        chassisBottom = bottom;
        chassisEdge = edge;
        return this;
    }

    private Theme rim(float glowStrength, int... colors) {
        glow = glowStrength;
        rim = colors;
        return this;
    }

    private Theme keys(int top, int bottom, int skirtColor, int socketColor, int socketEdgeColor) {
        capTop = top;
        capBottom = bottom;
        skirt = skirtColor;
        socket = socketColor;
        socketEdge = socketEdgeColor;
        return this;
    }

    private Theme ui(int accentColor, int textColor, int mutedColor, int cardColor, int cardHighColor) {
        accent = accentColor;
        text = textColor;
        muted = mutedColor;
        card = cardColor;
        cardHigh = cardHighColor;
        return this;
    }

    private Theme inset(int top, int bottom, int edge) {
        insetTop = top;
        insetBottom = bottom;
        insetEdge = edge;
        return this;
    }

    private Theme button(int top, int bottom, int edge) {
        buttonTop = top;
        buttonBottom = bottom;
        buttonEdge = edge;
        return this;
    }

    private Theme pads(int... colors) {
        pads = colors;
        return this;
    }

    static final Theme[] ALL = {
        new Theme(0, "Grafite", "Metal escuro discreto")
            .desk(0xFF1C1D21, 0xFF0C0D0F).chassis(0xFF3D3E43, 0xFF27282C, 0xFF5A5B61)
            .rim(0.45f, 0xFF8A8D96, 0xFF4A4C53, 0xFFB5B8C2, 0xFF4A4C53, 0xFF8A8D96)
            .keys(0xFF3A3B40, 0xFF232428, 0xFF141518, 0xFF0B0B0D, 0x38FFFFFF)
            .ui(0xFF4C8DFF, 0xFFF1F3F8, 0xFF9AA2B5, 0xFF24252A, 0xFF34353B)
            .inset(0xFF0E0F11, 0xFF18191C, 0xFF4A4B50).button(0xFF4A4B51, 0xFF2D2E32, 0xFF131416)
            .pads(0xFF63C9E8, 0xFFF2A972, 0xFF74D98F, 0xFFE28FEF, 0xFF4FA9EE, 0xFFF4F1EB, 0xFFEF7B76, 0xFFF3D173),
        new Theme(1, "Preto Absoluto", "Preto puro, ideal para telas AMOLED")
            .desk(0xFF000000, 0xFF000000).chassis(0xFF101012, 0xFF050506, 0xFF2A2A2E)
            .rim(0.30f, 0xFF3A3A40, 0xFF0F0F12, 0xFF6A6A72, 0xFF0F0F12, 0xFF3A3A40)
            .keys(0xFF1D1D21, 0xFF0B0B0D, 0xFF050506, 0xFF000000, 0x24FFFFFF)
            .ui(0xFFEDEDF2, 0xFFF5F5F7, 0xFF85858F, 0xFF131316, 0xFF1F1F24)
            .inset(0xFF000000, 0xFF08080A, 0xFF2C2C31).button(0xFF26262B, 0xFF121215, 0xFF000000)
            .pads(0xFFE9E9EE, 0xFF9FA3AE, 0xFF6FD3C7, 0xFFC7A4F0, 0xFF7FB2F2, 0xFFF0B98A, 0xFFEE8E9C, 0xFFB6E08A),
        new Theme(2, "RGB Gamer", "Luz da borda em todas as cores")
            .desk(0xFF141519, 0xFF050507).chassis(0xFF26272C, 0xFF111216, 0xFF45464C)
            .rim(1f, 0xFFFF3B6B, 0xFFFF9F2E, 0xFFF7F03A, 0xFF3CF27A, 0xFF2ED8FF, 0xFF7A5CFF, 0xFFFF3BD4, 0xFFFF3B6B)
            .keys(0xFF2C2D32, 0xFF17181B, 0xFF0C0D0F, 0xFF060607, 0x2AFFFFFF)
            .ui(0xFF3CF27A, 0xFFF3F5F9, 0xFF9AA0AE, 0xFF1E1F24, 0xFF2D2E34)
            .inset(0xFF08090B, 0xFF131417, 0xFF3C3D43).button(0xFF3A3B41, 0xFF1F2024, 0xFF0A0A0C)
            .pads(0xFF5ADCF2, 0xFFF5A66B, 0xFF62E58A, 0xFFEF8DF5, 0xFF39A8F5, 0xFFF7F4EF, 0xFFF2706C, 0xFFF7DB5E),
        new Theme(3, "Neon Cyber", "Magenta e ciano sobre violeta")
            .desk(0xFF170B2B, 0xFF07040F).chassis(0xFF291548, 0xFF120A22, 0xFF4B2C7D)
            .rim(0.95f, 0xFFFF2BD6, 0xFF7A3CFF, 0xFF19E3FF, 0xFF7A3CFF, 0xFFFF2BD6)
            .keys(0xFF2F1B50, 0xFF170D2A, 0xFF0D0719, 0xFF07030F, 0x30C9A6FF)
            .ui(0xFFFF2BD6, 0xFFF6EEFF, 0xFFA792C9, 0xFF22123B, 0xFF32205A)
            .inset(0xFF0A0516, 0xFF150B27, 0xFF55348C).button(0xFF412668, 0xFF21123B, 0xFF0B0516)
            .pads(0xFFFF45DC, 0xFF23E4FF, 0xFF9266FF, 0xFFFF6FA5, 0xFF3CF5CC, 0xFFFFC552, 0xFF5D8BFF, 0xFFF0E6FF),
        new Theme(4, "Matrix", "Verde fósforo sobre preto")
            .desk(0xFF04100A, 0xFF010403).chassis(0xFF0E2015, 0xFF040A07, 0xFF1E5236)
            .rim(0.85f, 0xFF00FF88, 0xFF0A5F3A, 0xFF7CFFB8, 0xFF0A5F3A, 0xFF00FF88)
            .keys(0xFF14291C, 0xFF07110C, 0xFF030806, 0xFF010302, 0x2E6CFFB0)
            .ui(0xFF00E676, 0xFFD9FFE9, 0xFF6FA88A, 0xFF0D1C14, 0xFF16301F)
            .inset(0xFF010503, 0xFF07110C, 0xFF1F5A3B).button(0xFF1C3B29, 0xFF0B1C13, 0xFF010403)
            .pads(0xFF00F07A, 0xFF7CFF5C, 0xFF23D6A8, 0xFFB6FF3C, 0xFF19B86A, 0xFFD8FFE6, 0xFF4CE0FF, 0xFFF0E85A),
        new Theme(5, "Tron", "Azul elétrico sobre marinho")
            .desk(0xFF07101F, 0xFF02050B).chassis(0xFF10223A, 0xFF050C18, 0xFF22507F)
            .rim(0.90f, 0xFF19C8FF, 0xFF0A4C8C, 0xFF9CE9FF, 0xFF0A4C8C, 0xFF19C8FF)
            .keys(0xFF182D47, 0xFF0A1525, 0xFF050B15, 0xFF02050B, 0x3065C8FF)
            .ui(0xFF19C8FF, 0xFFE6F6FF, 0xFF7E9DBB, 0xFF0F1D30, 0xFF1A2F49)
            .inset(0xFF02060D, 0xFF0A1423, 0xFF245A8C).button(0xFF20405F, 0xFF0E1E31, 0xFF02060C)
            .pads(0xFF22CFFF, 0xFFFF9A3C, 0xFF3D8BFF, 0xFF7CF0FF, 0xFFB98CFF, 0xFFEAF8FF, 0xFF2CE6C2, 0xFFFF6A5C),
        new Theme(6, "Aurora", "Verde água e violeta em degradê")
            .desk(0xFF07141C, 0xFF0B0A1E).chassis(0xFF143440, 0xFF1C1A3E, 0xFF3A6A78)
            .rim(0.85f, 0xFF35F5B5, 0xFF35B8F5, 0xFF9B6BFF, 0xFF35F5B5)
            .keys(0xFF1D3F4B, 0xFF171734, 0xFF0D0F22, 0xFF070914, 0x3070F0D0)
            .ui(0xFF35F5B5, 0xFFEAFBF7, 0xFF8FB3B5, 0xFF132B35, 0xFF1F3F4C)
            .inset(0xFF060E16, 0xFF11142A, 0xFF3A6B7A).button(0xFF28505E, 0xFF1A1E3C, 0xFF060A14)
            .pads(0xFF3DF2B8, 0xFF47B9F5, 0xFFA277FF, 0xFFF08CD6, 0xFF7CF2E0, 0xFFEFFBFF, 0xFFF7C46B, 0xFF6C90FF),
        new Theme(7, "Vulcão", "Brasa, laranja e carvão")
            .desk(0xFF1A0705, 0xFF080202).chassis(0xFF321710, 0xFF160807, 0xFF6B2C1C)
            .rim(0.95f, 0xFFFF3B1F, 0xFFFF9F1F, 0xFFFFD24A, 0xFFFF6A1F, 0xFFFF3B1F)
            .keys(0xFF38190F, 0xFF190A07, 0xFF0E0504, 0xFF070202, 0x30FF9F6B)
            .ui(0xFFFF7A1F, 0xFFFFF0E8, 0xFFBD9486, 0xFF2A120D, 0xFF3F1D14)
            .inset(0xFF0C0302, 0xFF1C0A07, 0xFF6F2F1E).button(0xFF55261A, 0xFF2A100B, 0xFF0B0302)
            .pads(0xFFFF5A36, 0xFFFFA733, 0xFFFFD84F, 0xFFFF7E6B, 0xFFE8456B, 0xFFFFF1DF, 0xFFFF9F7A, 0xFFC96BFF),
        new Theme(8, "Ouro Negro", "Preto com detalhes dourados")
            .desk(0xFF12100A, 0xFF050403).chassis(0xFF201C14, 0xFF0D0B07, 0xFF6B5A30)
            .rim(0.70f, 0xFFF6D27A, 0xFF8A6A25, 0xFFFFEDB8, 0xFF8A6A25, 0xFFF6D27A)
            .keys(0xFF2A2519, 0xFF12100A, 0xFF090805, 0xFF040302, 0x30F6D27A)
            .ui(0xFFE8C063, 0xFFFBF5E6, 0xFFB0A483, 0xFF1C1912, 0xFF2C281C)
            .inset(0xFF060503, 0xFF12100A, 0xFF6B5A30).button(0xFF3D3522, 0xFF1C1810, 0xFF060503)
            .pads(0xFFEFC768, 0xFFD99A5B, 0xFFF7EBCB, 0xFFBFA05A, 0xFFE8B4A0, 0xFF9CC2A8, 0xFFA8B8D8, 0xFFD97B5F),
        new Theme(9, "Gelo", "Claro, prata e azul")
            .desk(0xFFE9EDF3, 0xFFC6CFDB).chassis(0xFFF8FAFC, 0xFFDBE1EA, 0xFFFFFFFF)
            .rim(0.60f, 0xFF8FB8FF, 0xFFD6E4FF, 0xFF5D94FF, 0xFFD6E4FF, 0xFF8FB8FF)
            .keys(0xFFFBFCFE, 0xFFD3DAE4, 0xFFAEB7C4, 0xFFA2ACBA, 0x80FFFFFF)
            .ui(0xFF2F6FED, 0xFF1A2130, 0xFF5B6576, 0xFFFFFFFF, 0xFFE6EBF2)
            .inset(0xFFD8DEE7, 0xFFEEF1F6, 0xFFB4BDC9).button(0xFFFFFFFF, 0xFFD9DFE8, 0xFFA8B2BF)
            .pads(0xFF6FC5EC, 0xFFF4B07E, 0xFF7ADB98, 0xFFD99AF0, 0xFF6BA8F5, 0xFFFFFFFF, 0xFFF08A85, 0xFFF4D67C),
    };

    static {
        // Gelo: tela clara, com o nome escuro sobre um degrade claro
        Theme ice = ALL[9];
        ice.lcd = 0xFFEEF1F6;
        ice.lcdTint = 0.22f;
        ice.gloss = 0x66FFFFFF;
        ice.label = 0xFF1A2130;
        ice.labelShade = 0xD0F2F5F9;
        ice.mark = 0x38202B3C;
    }

    static Theme byId(int id) {
        return id >= 0 && id < ALL.length ? ALL[id] : ALL[0];
    }

    /** Mistura duas cores, de 0 (so a) a 1 (so b), mantendo o alfa de a. */
    static int mix(int a, int b, float t) {
        int ar = (a >> 16) & 255, ag = (a >> 8) & 255, ab = a & 255;
        int br = (b >> 16) & 255, bg = (b >> 8) & 255, bb = b & 255;
        return (a & 0xFF000000) | ((int) (ar + (br - ar) * t) << 16) | ((int) (ag + (bg - ag) * t) << 8) | (int) (ab + (bb - ab) * t);
    }

    static int alpha(int color, int alpha) {
        return (color & 0x00FFFFFF) | (alpha << 24);
    }

    static float luminance(int c) {
        return (0.299f * ((c >> 16) & 255) + 0.587f * ((c >> 8) & 255) + 0.114f * (c & 255)) / 255f;
    }
}
