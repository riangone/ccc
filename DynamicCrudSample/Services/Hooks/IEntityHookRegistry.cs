// ファイル概要: フック名からフック実装を検索するレジストリインターフェースを定義します。

namespace DynamicCrudSample.Services.Hooks;

/// <summary>
/// フック名（文字列）から IEntityHook 実装を取得するレジストリ。
/// DI に登録された全 IEntityHook を管理します。
/// </summary>
public interface IEntityHookRegistry
{
    /// <summary>
    /// フック名に対応するフックを返します。
    /// 見つからない場合は null を返します（登録漏れ時はログ警告を出すこと）。
    /// </summary>
    IEntityHook? Find(string name);
}
