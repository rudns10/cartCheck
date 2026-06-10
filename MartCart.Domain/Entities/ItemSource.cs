namespace MartCart.Domain.Entities;

public enum ItemSource
{
    Ocr = 0,
    Manual = 1,
    AiAssisted = 2,
    Heuristic = 3,
}

public enum NameSource
{
    Ocr = 0,
    Manual = 1,
    AiAssisted = 2,
    ProductDb = 3,
}
