using Npgsql;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace PIBotRemake
{
    public static class Configs
    {
        public static string MyServer = "47.93.57.125";
        public static string MyLogin = "MyLogin";
        public static string MyPass = "MyPass";
        public static string MyDatabase = "postgres";
        public static string ConnectionStr = $"Host={MyServer};Username={MyLogin};Password={MyPass};Port=5432;Database={MyDatabase}";
    }

    public class BasicActions
    {
        public int GetUserID(string userQQ)
        {
            string query = $"SELECT userid FROM public.player WHERE qqnumber=@qq";
            using (var connection = new NpgsqlConnection(Configs.ConnectionStr))
            {
                using (var command = new NpgsqlCommand(query, connection))
                {
                    command.CommandText = query;
                    command.Parameters.AddWithValue("@qq", userQQ);

                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return reader.GetInt32(reader.GetOrdinal("userid"));
                        }
                        else
                        {
                            return -1;
                        }
                    }
                }
            }
        }

        public int GetSingleScore(int userID, int scenarioID)
        {
            string query = $"SELECT score FROM public.score WHERE userid=@userid AND scenarioid=@scenarioid";
            using (var connection = new NpgsqlConnection(Configs.ConnectionStr))
            {
                using (var command = new NpgsqlCommand(query, connection))
                {
                    command.CommandText = query;
                    command.Parameters.AddWithValue("@userid", userID);
                    command.Parameters.AddWithValue("@scenarioid", scenarioID);

                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return reader.GetInt32(reader.GetOrdinal("score"));
                        }
                        else
                        {
                            return 0;
                        }
                    }
                }
            }
        }

        public Dictionary<int, int> GetAllScores(int userID)
        {
            string query = $"SELECT scenarioid, score FROM public.score WHERE userid=@userid";
            using (var connection = new NpgsqlConnection(Configs.ConnectionStr))
            {
                using (var command = new NpgsqlCommand(query, connection))
                {
                    command.CommandText = query;
                    command.Parameters.AddWithValue("@userid", userID);

                    using (var reader = command.ExecuteReader())
                    {
                        Dictionary<int, int> scores = new Dictionary<int, int>();
                        if (reader.Read())
                        {
                            do
                            {
                                int scenarioid = reader.GetInt32(reader.GetOrdinal("scenarioid"));
                                int score = reader.GetInt32(reader.GetOrdinal("score"));
                                scores.Add(scenarioid, score);
                            } while (reader.Read());
                            return scores;
                        }
                        else
                        {
                            return null;
                        }
                    }
                }
            }
        }

        public float CalculateSinglePTT(int score, int scenarioID)
        {
            float constant = 0f;
            string query = $"SELECT constant FROM public.scenario WHERE scenarioid=@scenarioid";
            using (var connection = new NpgsqlConnection(Configs.ConnectionStr))
            {
                using (var command = new NpgsqlCommand(query, connection))
                {
                    command.CommandText = query;
                    command.Parameters.AddWithValue("@scenarioid", scenarioID);

                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            constant = reader.GetFloat(reader.GetOrdinal("constant"));
                        }
                    }
                }
            }

            float ptt;
            if (score >= 75000)
                ptt = constant + 2;
            else if (score >= 55000)
                ptt = constant + (score - 55000) / 10000;
            else if (score >= 30000)
                ptt = constant - 5 + (score - 30000) / 5000;
            else
                ptt = constant - 8 + score / 10000;

            return ptt > 0 && constant >= 1.0 ? ptt : 0;
        }

        public float CalculateTotalPTT(int userID)
        {
            Dictionary<int, int> scores = GetAllScores(userID);
            if (scores == null || scores.Count == 0)
                return 0;
            else
            {
                Dictionary<int, float> PTTs = new Dictionary<int, float>();
                foreach (var score in scores)
                {
                    PTTs.Add(score.Key, CalculateSinglePTT(score: score.Value, scenarioID: score.Key));
                }
                var top30 = PTTs.OrderByDescending(kvp => kvp.Value).Take(30).ToList();
                float average = top30.Average(kvp => kvp.Value);
                return average;
            }
        }

        public float GetTotalPTT(int userID)
        {
            string query = $"SELECT potential FROM public.player WHERE userid=@userid";
            using (var connection = new NpgsqlConnection(Configs.ConnectionStr))
            {
                using (var command = new NpgsqlCommand(query, connection))
                {
                    command.CommandText = query;
                    command.Parameters.AddWithValue("@userid", userID);

                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return reader.GetFloat(reader.GetOrdinal("potential"));
                        }
                        else
                        {
                            return 0;
                        }
                    }
                }
            }
        }

        public int GetRank(int userID)
        {
            string query = $"SELECT rank FROM public.player WHERE userid=@userid";
            using (var connection = new NpgsqlConnection(Configs.ConnectionStr))
            {
                using (var command = new NpgsqlCommand(query, connection))
                {
                    command.CommandText = query;
                    command.Parameters.AddWithValue("@userid", userID);

                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return reader.GetInt32(reader.GetOrdinal("rank"));
                        }
                        else
                        {
                            return 0;
                        }
                    }
                }
            }
        }

        public Int32 GetTTS(int userID)
        {
            string query = $"SELECT scoresum FROM public.player WHERE userid=@userid";
            using (var connection = new NpgsqlConnection(Configs.ConnectionStr))
            {
                using (var command = new NpgsqlCommand(query, connection))
                {
                    command.CommandText = query;
                    command.Parameters.AddWithValue("@userid", userID);

                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return reader.GetInt32(reader.GetOrdinal("scoresum"));
                        }
                        else
                        {
                            return 0;
                        }
                    }
                }
            }
        }

        //The ranking is ptt!
        [Obsolete]
        public void UpdateAllRankings()
        {
            string query = $"SELECT userid, potential FROM public.player";
            Dictionary<int, float> ptts = new Dictionary<int, float>();
            using (var dataSource = NpgsqlDataSource.Create(Configs.ConnectionStr))
            {
                using (var command = dataSource.CreateCommand())
                {
                    command.CommandText = query;
                    command.ExecuteNonQuery();
                }
            }

            var pttList = ptts.OrderByDescending(kvp => kvp.Value).ToList();

            using (var dataSource = NpgsqlDataSource.Create(Configs.ConnectionStr))
            {
                for (int i = 0; i < ptts.Count; i++)
                {
                    string query1 = $"UPDATE public.player SET rank = {i + 1} WHERE userid=@userid";

                    using (var command = dataSource.CreateCommand())
                    {
                        command.CommandText = query;
                        command.Parameters.AddWithValue("@userid", pttList[i].Key);
                        command.ExecuteNonQuery();
                    }
                }
            }
        }

        public void Submit(string _cmd)
        {
            // The command is ["call","submit","scenarioLib",userQQ,scenarioID,newScore], split by char ',', and no "[]" or "\"".
            string[] cmd = _cmd.Split(',');
            string qq = cmd[3];
            string scen = cmd[4];
            string score = cmd[5];
            int id = GetUserID(qq);

            if (id == -1)
            {
                Trace.TraceInformation($"QQ '{qq}' wasn't found in public.player!");
                return;
            }
            else if (string.IsNullOrEmpty(scen) || string.IsNullOrEmpty(score))
            {
                Trace.TraceInformation($"QQ '{qq}' didn't give all the arguments of the command!");
            }
            else
            {
                Trace.TraceInformation($"QQ '{qq}' submitted score; ScenCode: {scen}; Score: {score}.");
                int OriginalScore = GetSingleScore(id, int.Parse(scen));
                if (OriginalScore >= int.Parse(score)) return;
                else
                {
                    using (var connection = new NpgsqlConnection(Configs.ConnectionStr))
                    {
                        string query = $"UPDATE public.score SET score = @score WHERE userid=@userID";
                        using (var command = new NpgsqlCommand(query, connection))
                        {
                            command.CommandText = query;
                            command.Parameters.AddWithValue("@score", score);
                            command.Parameters.AddWithValue("@userID", id);
                            command.ExecuteNonQuery();
                        }

                        string query1 = "INSERT INTO public.submissionhistory " +
                                    "(userid, scenarioid, submitdate) " +
                                    "VALUES (@userid, @scenarioid, @submitdate)";
                        using (var command = new NpgsqlCommand(query1, connection))
                        {
                            command.CommandText = query1;
                            command.Parameters.AddWithValue("@userid", id);
                            command.Parameters.AddWithValue("@scenarioid", scen);
                            command.Parameters.AddWithValue("@submitdate", DateTime.Today);
                            command.ExecuteNonQuery();
                        }

                        int rank = GetRank(id);
                        float ptt = GetTotalPTT(id);
                        int tts = GetTTS(id);

                        string query2 = "UPDATE public.submissionhistory " +
                            "SET newrank=@newrank, newpotential=@newpotential, newtotalscore=@newtotalscore " +
                            "WHERE submissionid IN " +
                            "(SELECT submissionid FROM public.submissionhistory ORDER BY submissionid DESC LIMIT 1)";
                        using (var command = new NpgsqlCommand(query2, connection))
                        {
                            command.CommandText = query2;
                            command.Parameters.AddWithValue("@newrank", rank);
                            command.Parameters.AddWithValue("@newpotential", ptt);
                            command.Parameters.AddWithValue("@newtotalscore", tts);
                            command.ExecuteNonQuery();
                        }
                    }
                }
            }
        }

        public void ChangeNickname(string _cmd)
        {
            // The command is ["call","ChangeNickname",userQQ,nickname], split by char ',', and no "[]" or "\"".
            string[] cmd = _cmd.Split(',');
            string qq = cmd[2];
            string nickname = cmd[3];

            int id = GetUserID(qq);
            if (id == -1)
            {
                Trace.TraceInformation($"QQ '{qq}' wasn't found in public.player!");
                return;
            }
            else if (nickname == null)
            {
                Trace.TraceInformation($"QQ '{qq}' didn't type his nickname.");
                return;
            }
            else
            {
                Trace.TraceInformation($"QQ '{qq}' changed his nickname: {nickname}.");
                string query = $"UPDATE public.player SET nickname = @nickname WHERE userid = @id";
                using (var connection = new NpgsqlConnection(Configs.ConnectionStr))
                {
                    using (var command = new NpgsqlCommand(query, connection))
                    {
                        command.CommandText = query;
                        command.Parameters.AddWithValue("nickname", nickname);
                        command.Parameters.AddWithValue("@id", id);
                        command.ExecuteNonQuery();
                    }
                }
            }
        }

        public void Register(string _cmd)
        {
            // The command is ["call","Register",userQQ,nickname], split by char ',', and no "[]" or "\"".
            string[] cmd = _cmd.Split(',');
            string qq = cmd[2];
            string nickname = cmd[3];
            int id = GetUserID(qq);

            if (id != -1)
            {
                Trace.TraceInformation($"QQ '{qq}' tried to register, but he had registered before!");
                return;
            }
            else
            {
                string query = "INSERT INTO public.player (qqnumber, nickname) VALUES (@qq, @nickname)";
                using (var connection = new NpgsqlConnection(Configs.ConnectionStr))
                {
                    using (var command = new NpgsqlCommand(query, connection))
                    {
                        command.CommandText = query;
                        command.Parameters.AddWithValue("@qq", qq);
                        command.Parameters.AddWithValue("@nickname", nickname);
                        command.ExecuteNonQuery();
                    }
                }
            }
        }

        public void OpChangeScore(string _cmd)
        {
            // The command is ["call","OpChangeScore",userQQ,targetQQ,targetScenario,targetScore], split by char ',', and no "[]" or "\"".

            string[] cmd = _cmd.Split(',');
            string qq = cmd[2];
            string targetQQ = cmd[3];
            string targetScenarioID = cmd[4];
            string targetScore = cmd[5];

            int id = GetUserID(qq);

            string query = $"SELECT opid FROM public.operator WHERE opqq = @qq";
            using (var connection = new NpgsqlConnection(Configs.ConnectionStr))
            {
                using (var command = new NpgsqlCommand(query, connection))
                {
                    command.CommandText = query;
                    command.Parameters.AddWithValue("@qq", qq);
                    using (var reader = command.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            Trace.TraceInformation($"QQ '{qq}' is not an operator!");
                            return;
                        }
                    }
                }
            }

            if (id == -1)
            {
                Trace.TraceInformation($"QQ '{qq}' wasn't found in public.player!");
                return;
            }
            else if (string.IsNullOrEmpty(targetScenarioID) || string.IsNullOrEmpty(targetScore))
            {
                Trace.TraceInformation($"QQ '{qq}' didn't give all the arguments of the command!");
            }
            else
            {
                Trace.TraceInformation($"Operation '{qq}' changed {targetQQ}'s score of {targetScenarioID} to {targetScore}! ");
                int OriginalScore = GetSingleScore(id, int.Parse(targetScenarioID));
                if (OriginalScore >= int.Parse(targetScore)) return;
                else
                {
                    using (var connection = new NpgsqlConnection(Configs.ConnectionStr))
                    {
                        string query1 = "UPDATE public.score SET score = @targetScore WHERE userid=@userID";
                        using (var command = new NpgsqlCommand(query1, connection))
                        {
                            command.CommandText = query1;
                            command.Parameters.AddWithValue("@targetScore", targetScore);
                            command.Parameters.AddWithValue("@userID", id);
                            command.ExecuteNonQuery();
                        }


                        string query2 = "INSERT INTO public.submissionhistory " +
                                    "(userid, scenarioid, submitdate) " +
                                    "VALUES (@userid, @scenarioid, @submitdate)";
                        using (var command = new NpgsqlCommand(query2, connection))
                        {
                            command.CommandText = query2;
                            command.Parameters.AddWithValue("@userid", id);
                            command.Parameters.AddWithValue("@scenarioid", targetScenarioID);
                            command.Parameters.AddWithValue("@submitdate", DateTime.Today);
                            command.ExecuteNonQuery();
                        }

                        int rank = GetRank(id);
                        float ptt = GetTotalPTT(id);
                        int tts = GetTTS(id);

                        string query3 = "UPDATE public.submissionhistory " +
                            "SET newrank=@newrank, newpotential=@newpotential, newtotalscore=@newtotalscore " +
                            "WHERE submissionid IN " +
                            "(SELECT submissionid FROM public.submissionhistory ORDER BY submissionid DESC LIMIT 1)";
                        using (var command = new NpgsqlCommand(query3, connection))
                        {
                            command.CommandText = query3;
                            command.Parameters.AddWithValue("@newrank", rank);
                            command.Parameters.AddWithValue("@newpotential", ptt);
                            command.Parameters.AddWithValue("@newtotalscore", tts);
                            command.ExecuteNonQuery();
                        }
                    }
                }
            }
        }

        public void OpBlock(string _cmd)
        {
            // The command is ["call","block",userQQ,targetQQ], split by char ',', and no "[]" or "\"".
            string[] cmd = _cmd.Split(',');
            string qq = cmd[2];
            string targetQQ = cmd[3];

            int id = GetUserID(qq);

            string query = $"SELECT opid FROM public.operator WHERE opqq = @qq";
            using (var connection = new NpgsqlConnection(Configs.ConnectionStr))
            {
                using (var command = new NpgsqlCommand(query, connection))
                {
                    command.CommandText = query;
                    command.Parameters.AddWithValue("@qq", qq);
                    using (var reader = command.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            Trace.TraceInformation($"QQ '{qq}' is not an operator!");
                            return;
                        }
                    }
                }
            }

            string query1 = $"UPDATE public.player SET banned = true WHERE userid = @id";
            using (var connection = new NpgsqlConnection(Configs.ConnectionStr))
            {
                using (var command = new NpgsqlCommand(query1, connection))
                {
                    command.CommandText = query1;
                    command.Parameters.AddWithValue("@id", id);
                    command.ExecuteNonQuery();
                }
            }
        }

        public void OpUnblock(string _cmd)
        {
            // The command is ["call","Unblock",userQQ,targetQQ], split by char ',', and no "[]" or "\"".
            string[] cmd = _cmd.Split(',');
            string qq = cmd[2];
            string targetQQ = cmd[3];

            int id = GetUserID(qq);

            string query = $"SELECT opid FROM public.operator WHERE opqq = @qq";
            using (var connection = new NpgsqlConnection(Configs.ConnectionStr))
            {
                using (var command = new NpgsqlCommand(query, connection))
                {
                    command.CommandText = query;
                    using (var reader = command.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            Trace.TraceInformation($"QQ '{qq}' is not an operator!");
                            return;
                        }
                    }
                }
            }

            string query1 = $"UPDATE public.player SET banned = false WHERE userid = @id";
            using (var connection = new NpgsqlConnection(Configs.ConnectionStr))
            {
                using (var command = new NpgsqlCommand(query1, connection))
                {
                    command.CommandText = query1;
                    command.Parameters.AddWithValue("@id", id);
                    command.ExecuteNonQuery();
                }
            }
        }

        public void OpAddAlias(string _cmd)
        {
            // The command is ["call","addAlias",userQQ,targetScenario,alias], split by char ',', and no "[]" or "\"".
            string[] cmd = _cmd.Split(',');
            string qq = cmd[2];
            int scenarioid = int.Parse(cmd[3]);
            string alias = cmd[4];

            int id = GetUserID(qq);

            string query = $"SELECT opid FROM public.operator WHERE opqq = @qq";
            using (var connection = new NpgsqlConnection(Configs.ConnectionStr))
            {
                using (var command = new NpgsqlCommand(query, connection))
                {
                    command.CommandText = query;
                    command.Parameters.AddWithValue("@qq", qq);
                    using (var reader = command.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            Trace.TraceInformation($"QQ '{qq}' is not an operator!");
                            return;
                        }
                    }
                }
            }

            string query1 = "INSERT INTO scenarioalias (scenarioid, alias) VALUES (@scenarioid, @alias) " +
               "ON CONFLICT (scenarioid, alias) DO NOTHING;";
            using (var connection = new NpgsqlConnection(Configs.ConnectionStr))
            {
                using (var command = new NpgsqlCommand(query1, connection))
                {
                    command.CommandText = query1;
                    command.Parameters.AddWithValue("@scenarioid", scenarioid);
                    command.Parameters.AddWithValue("@alias", alias);
                    command.ExecuteNonQuery();
                }
            }
        }

        public void OpRemoveAlias(string _cmd)
        {
            // The command is ["call","removeAlias",userQQ,targetScenario,alias], split by char ',', and no "[]" or "\"".
            string[] cmd = _cmd.Split(',');
            string qq = cmd[2];
            int scenarioid = int.Parse(cmd[3]);
            string alias = cmd[4];

            int id = GetUserID(qq);

            string query = $"SELECT opid FROM public.operator WHERE opqq = @qq";
            using (var connection = new NpgsqlConnection(Configs.ConnectionStr))
            {
                using (var command = new NpgsqlCommand(query, connection))
                {
                    command.CommandText = query;
                    command.Parameters.AddWithValue("@qq", qq);
                    using (var reader = command.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            Trace.TraceInformation($"QQ '{qq}' is not an operator!");
                            return;
                        }
                    }
                }
            }

            string checkQuery = "SELECT COUNT(*) FROM scenario_aliases WHERE scenarioid = @scenarioid AND alias = @alias";
            string deleteQuery = "DELETE FROM scenario_aliases WHERE scenarioid = @scenarioid AND alias = @alias";
            using (var connection = new NpgsqlConnection(Configs.ConnectionStr))
            {
                using (var command = new NpgsqlCommand(query, connection))
                {
                    command.CommandText = checkQuery;
                    command.Parameters.AddWithValue("@scenarioid", scenarioid);
                    command.Parameters.AddWithValue("@alias", alias);

                    int count = Convert.ToInt32(command.ExecuteScalar());

                    if (count > 0)
                    {
                        Trace.TraceInformation($"Alias '{alias}' of scenarioID: '{scenarioid}' successfully deleted!");
                        command.CommandText = deleteQuery;
                        int rowsAffected = command.ExecuteNonQuery();
                    }
                    else
                    {
                        Trace.TraceInformation($"No aliases found for scenarioID: {scenarioid}!");
                        return;
                    }
                }
            }
        }
    }
}