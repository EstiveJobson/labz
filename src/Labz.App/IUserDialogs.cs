namespace Labz.App;

public interface IUserDialogs
{
    bool Confirm(string message);

    string? ChooseFolder(string title);

    void Alert(string message);
}
