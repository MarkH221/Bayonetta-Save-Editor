using Isolite.IOPackage;
using Isolite.PS3FileSystem;
using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace BayonettaSE
{
    public partial class Form1 : Form
    {
        /// <summary>
        /// The target filename, typically inside a ps3 save folder there is usually one file of
        /// particular interest the rest are background noise.
        /// </summary>
        private const string FileName = "CNTDAT";

        /// <summary>
        /// We store savedata in a byte array for easy manipulation. You could just edit the file
        /// directly if you really wanted.
        /// </summary>
        private byte[] _buffer;

        /// <summary>Keeps track of the path for the savedata so we can save over it later.</summary>
        private string _filepath, _file;

        /// <summary>
        /// PS3 requires manipulation to get at the raw data and comes as a folder so we keep track.
        /// </summary>
        private bool _isps3 = true;

        //An instance of the savemanager so we can use it.
        private Ps3SaveManager _manager;

        /// <summary>
        /// Determines whether to handle decryption or not inside the editor itself, purely to not
        /// step on the users toes if they're using another application. PS3 only.
        /// </summary>
        private bool _predecrypted;

        /// <summary>This is the PS3 savedata encryption key for this game.</summary>
        private readonly byte[] _key =
        {
            0x41, 0x42, 0x43, 0x44, 0x45, 0x46, 0x46, 0x46, 0x46, 0x46, 0x46, 0x46, 0x46,
            0x46, 0x46, 0x00
        };

        public Form1()
        {
            InitializeComponent();
            //Comboboxes should be initialised fully as well to ensure they have contents, they misbehave otherwise.
            difficultybox.SelectedIndex = 0;
        }

        private void Read()
        {
            //Read operation using functions from isolite library, a normal stream can be used but isolite has suitable reading functions.
            using (var r = new RWStream(_buffer, true))
            {
                //Difficulty
                r.Position = 0x33;
                difficultybox.SelectedIndex = r.ReadUInt8();
                //Halos
                r.Position = 0x0000EF44;
                //unsigned integer because you don't get negative halo ever, though it might actually be signed in the save which just limits the max value really.
                var t = r.ReadUInt32();
                //I realise we read it as a uint32 and are now limiting it based on int32, in this particular scenario it doesn't matter.
                halobox.Value = (t > Int32.MaxValue) ? Int32.MaxValue : t;
            }
        }

        private void PS3Open(object sender, EventArgs e)
        {
            //PS3 save manipulation typically requires the entire save folder, even if we only want one file from it.
            using (var o = new FolderBrowserDialog { Description = "Navigate to Bayonetta '-CNT' PS3 Save Folder" })
            {
                //ShowDialog returns a dialogresult depending on what happens to the dialog, if the user didn't press ok you can be sure a file hasn't been chosen.
                if (o.ShowDialog() != DialogResult.OK) return;
                _filepath = o.SelectedPath;
                _manager = new Ps3SaveManager(_filepath, _key);
                //Check for telltale sign of decryption via pfdtool or any files with broken hashes, sign of decryption or unfixed tampering.
                if (Directory.GetFiles(_filepath).Any(i => i.Contains("~files_decrypted_by_pfdtool")) ||
                    _manager.Files.Any(i => i.IsEncrypted == false))
                {
                    _predecrypted = true;
                    _file = _filepath + @"\" + FileName;
                    _buffer = File.ReadAllBytes(_file);
                }
                else
                {
                    _predecrypted = false;
                    var files = _manager.Files.FirstOrDefault(t => t.PFDEntry.file_name == FileName);
                    if (files != null) _buffer = files.DecryptToBytes();
                }
                _isps3 = true;
            }

            Read();
        }

        private void Save(object sender, EventArgs e)
        {
            //New checksum variable.
            var c = 0;
            using (var r = new RWStream(_buffer, true))
            {
                //Halo
                r.Position = 0x0000EF44;
                r.WriteUInt32((uint)halobox.Value);
                //Write Items
                if (concheckbox.Checked)
                {
                    r.Position = 0x0000EF5A;
                    for (var i = 0; i < 16; i++)
                    {
                        r.WriteInt32(9999);
                    }
                }

                //Difficulty
                r.Position = 0x33;
                r.WriteUInt8((byte)difficultybox.SelectedIndex);

                //Bayonetta's checksum is located at 0x14 and is 4 bytes.
                //The checksum is for 0x18 -> EOF
                //Compute checksum
                r.Position = 0x18;
                do
                {
                    //XOR!
                    c ^= r.ReadInt32();
                } while (r.Position < _buffer.Length);
                //Write new checksum to buffer.
                r.Position = 0x14;
                r.WriteInt32(c);
            }
            //Write edited buffer to file.
            File.WriteAllBytes(_file, _buffer);
            if (_isps3)
            {
                //Rebuild & Encrypt if was decrypted in editor, otherwise leave to whatever tool user used.
                if (!_predecrypted) _manager.ReBuildChanges(true);
            }
            //Let the user know it worked, tell them the checksum for no reason just because you can.
            MessageBox.Show("Saving Complete.\nNew Checksum: 0x" + c.ToString("X8"));
        }

        private void WiiUOpen(object sender, EventArgs e)
        {
            //Due to the nature of save extraction from a wiiu any platform specific annoyances have been taken care of already, it's the raw save at this point.
            using (var o = new OpenFileDialog())
            {
                if (o.ShowDialog() != DialogResult.OK) return;
                _file = o.FileName;
                _buffer = File.ReadAllBytes(_file);
                _isps3 = false;
                Read();
            }
        }

        private void MaxHalos(object sender, EventArgs e)
        {                         //Technically not the max but close enough.
            halobox.Value = int.MaxValue;
        }
    }
}