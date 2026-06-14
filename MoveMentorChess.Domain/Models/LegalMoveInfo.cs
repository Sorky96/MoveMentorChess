namespace MoveMentorChess.Domain;

public sealed record LegalMoveInfo(
    string Uci,
    string San,
    string FromSquare,
    string ToSquare,
    string MovingPiece,
    string? PromotionPiece,
    bool IsCapture,
    bool IsEnPassant,
    bool IsCastle);
