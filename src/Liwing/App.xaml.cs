using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Windows;

namespace Liwing
{
	/// <summary>
	/// App.xaml の相互作用ロジック
	/// </summary>
	public partial class App : Application
	{
		/// <summary>
		/// カスタムURLスキーマ
		/// </summary>
		const string CustomURLSchema = "lw:";

		/// <summary>
		/// URLエンコードを指定するパラメータ文字列
		/// </summary>
		const string URLEncodeParam = "-e";

		/// <summary>
		/// Slackでリンクが途切れやすい文字のみURLエンコードするパラメータ文字列
		/// </summary>
		const string SlackSafeEncodeParam = "-s";

		/// <summary>
		/// コピーするURLのエンコード方法
		/// </summary>
		private enum UrlEncodeMode
		{
			None,
			Full,
			SlackSafe,
		}

		/// <summary>
		/// Fileスキーマ
		/// </summary>
		private const string FileSchema = "//file/";

		/// <summary>
		/// 半角スペースのエンコード文字列
		/// </summary>
		private static readonly string SpaceEncodeValue = HttpUtility.UrlEncode(" ");

		/// <summary>
		/// Slackの自動リンク判定でURLが途切れやすいため、SlackSafeモードでURLエンコードする文字
		/// </summary>
		private static readonly HashSet<char> SlackSafeEncodeCharacters = new HashSet<char>
		{
			'・', '、', '。', '，', '．',
			'「', '」', '『', '』', '（', '）', '【', '】', '〔', '〕',
			'《', '》', '〈', '〉',
			'！', '？', '：', '；',
			'～',
			'　',
			' ',
		};

		/// <summary>
		/// アプリケーションの開始
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void App_OnStartup(object sender, StartupEventArgs e)
		{
			try
			{
				// コマンドライン引数を取得
				// 1件目にアプリケーション自体のファイルパスが入り、指定された引数は2つ目以降に入る
				string[] args = Environment.GetCommandLineArgs();

				// 引数がなければ何もしない
				if (args.Length <= 1)
				{
#if DEBUG
					MessageBox.Show($"引数なしは終了");
#endif
					return;
				}

				// 以下、引数が指定された場合

				// [送る]メニューから複数のファイルを指定された場合、
				// 複数のファイルパスが指定されているため、それに対応するように各引数に対して実行する
				UrlEncodeMode urlEncodeMode = UrlEncodeMode.None;
				StringBuilder copyString =  new StringBuilder();
				for (int index = 1; index < args.Length; index++)
				{
					var arg = args[index];
					if (arg.ToLower().StartsWith(CustomURLSchema))
					{
						// カスタムURLを指定している場合は、パスのファイルを実行
						ExecuteFile(arg);
					}
					else if (arg == URLEncodeParam)
					{
						// URLエンコードを指定するパラメータが指定されている場合は、以降のパスをURLエンコードする
						urlEncodeMode = UrlEncodeMode.Full;
					}
					else if (arg == SlackSafeEncodeParam)
					{
						// Slackでリンクが途切れやすい文字のみURLエンコードする
						urlEncodeMode = UrlEncodeMode.SlackSafe;
					}
					else
					{
						// カスタムURLでなければ、クリップボードにコピーするためのファイルパス文字列を取得する
						var copyFilePath = GetCopyFilePath(arg, urlEncodeMode);
						copyString.AppendLine(copyFilePath);
					}
				}

				// コピー対象の文字列が存在する場合、クリップボードにコピーする
				if (copyString.Length > 0)
				{
					Clipboard.SetData(DataFormats.Text, copyString);
				}
			}
			finally
			{
				// 必ずアプリケーションを終了する
				Shutdown();
			}
		}

		/// <summary>
		/// 引数に設定されたパスのファイルを実行する
		/// </summary>
		/// <param name="arg">カスタムURLスキーマで指定された引数</param>
		private static void ExecuteFile(string arg)
		{
			// カスタムURLを取り除いた文字列をファイルパスとみなして取得
			var encodeUrl = arg.Substring(CustomURLSchema.Length);
			if (encodeUrl.ToLower().StartsWith(FileSchema))
			{
				encodeUrl = encodeUrl.Substring(FileSchema.Length);
			}

			// URL での転送用にエンコードされた文字列をデコードされた文字列に変換
			string decodeUrl = HttpUtility.UrlDecode(encodeUrl);
			var filePath = decodeUrl.Replace("/", "\\");


			if (string.IsNullOrEmpty(filePath) || (!File.Exists(filePath) && !Directory.Exists(filePath)))
			{
				MessageBox.Show($"以下のファイルが見つかりませんでした。{Environment.NewLine}{filePath}");
				return;
			}

			// 対象ファイルを実行
			var process = new Process
			{
				StartInfo =
				{
					UseShellExecute = true,
					FileName = filePath,
				}
			};
			process.Start();
		}

		/// <summary>
		/// 指定された引数を、コピーする文字列に変換する
		/// </summary>
		/// <param name="arg">指定された引数</param>
		/// <param name="urlEncodeMode">URLエンコード方法</param>
		private static string GetCopyFilePath(string arg, UrlEncodeMode urlEncodeMode)
		{
			// カスタムURL指定でない場合
			// 送られた引数がファイルパスだと仮定して、そのパスをカスタムURLスキーマに変換した文字列をクリップボードにコピーする

			// URL形式とするため、バックスラッシュをスラッシュに変換
			var urlFilePath = arg.Replace("\\", "/");

			// 指定されたエンコード方法でURL文字列を変換
			var encodeUrl = EncodeUrlFilePath(urlFilePath, urlEncodeMode);

			// カスタムURLスキーマとfileスキーマを加える
			var copyString = $"{CustomURLSchema}{FileSchema}{encodeUrl}";
#if DEBUG
			MessageBox.Show($"Clipboard Copy : {copyString}");
#endif
			return copyString;
		}

		/// <summary>
		/// 指定されたエンコード方法でURL用のファイルパスをエンコードする
		/// </summary>
		/// <param name="urlFilePath">URL形式に変換済みのファイルパス</param>
		/// <param name="urlEncodeMode">URLエンコード方法</param>
		private static string EncodeUrlFilePath(string urlFilePath, UrlEncodeMode urlEncodeMode)
		{
			switch (urlEncodeMode)
			{
				case UrlEncodeMode.Full:
					return HttpUtility.UrlEncode(urlFilePath);
				case UrlEncodeMode.SlackSafe:
					return EncodeSlackSafeCharacters(urlFilePath);
				default:
					// エンコードしない場合は、半角スペースのみエンコードする(スペースのままだと、引数として渡す時に分離されてしまうため)
					return urlFilePath.Replace(" ", SpaceEncodeValue);
			}
		}

		/// <summary>
		/// Slackの自動リンク判定でURLが途切れやすい文字のみURLエンコードする
		/// </summary>
		/// <param name="urlFilePath">URL形式に変換済みのファイルパス</param>
		private static string EncodeSlackSafeCharacters(string urlFilePath)
		{
			var encodedUrlFilePath = new StringBuilder();
			foreach (var character in urlFilePath)
			{
				encodedUrlFilePath.Append(SlackSafeEncodeCharacters.Contains(character)
					? HttpUtility.UrlEncode(character.ToString())
					: character.ToString());
			}

			return encodedUrlFilePath.ToString();
		}

	}
}
