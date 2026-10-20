using SlideDevPresenter.Core.Models;

namespace SlideDevPresenter.Core.Services;

public interface ICodeEditorService
{
    IReadOnlyList<CodeEditorEdition> GetAvailableEditions();
    void OpenProject(string projectDirectory, CodeEditorEdition edition);
}
