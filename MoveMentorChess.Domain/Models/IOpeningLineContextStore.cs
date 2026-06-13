namespace MoveMentorChess.Domain;

public interface IOpeningLineContextStore
{
    IReadOnlyList<string> GetOpeningValidationMoves(OpeningPositionKey rootPositionKey);
    IReadOnlyList<OpeningLineMove> GetOpeningPathLineMoves(OpeningPositionKey rootPositionKey);
}
