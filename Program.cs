using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

class Program
{
    static string dbFile = "tareas.db";
    static string connectionString = $"Data Source={dbFile};Version=3;";

    static void Main()
    {
        if (!File.Exists(dbFile))
        {
            CrearBaseDeDatos();
        }

        while (true)
        {
            Console.Clear();
            programName();
            Console.WriteLine();
            Console.WriteLine("Gestor de Tareas");
            Console.WriteLine("1. Agregar Tarea");
            Console.WriteLine("2. Seleccionar Tarea");
            Console.WriteLine("3. Mostrar Tareas");
            Console.WriteLine("4. Eliminar Tarea");
            Console.WriteLine("5. Salir");

            switch (Console.ReadLine())
            {
                case "1":
                    AgregarTarea();
                    break;
                case "2":
                    SeleccionarTarea();
                    break;
                case "3":
                    MostrarTareas();
                    break;
                case "4":
                    EliminarTarea();
                    break;
                case "5":
                    return;
                default:
                    Console.WriteLine("Opción no válida.");
                    Console.ReadKey();
                    break;
            }
        }
    }

    static void CrearBaseDeDatos()
    {
        using (var conn = new SQLiteConnection(connectionString))
        {
            conn.Open();

            string sqlTareas = "CREATE TABLE IF NOT EXISTS tareas (id INTEGER PRIMARY KEY, nombre TEXT, tiempo INTEGER)";
            using (var cmd = new SQLiteCommand(sqlTareas, conn))
            {
                cmd.ExecuteNonQuery();
            }

            string sqlSesiones = "CREATE TABLE IF NOT EXISTS sesiones (id INTEGER PRIMARY KEY AUTOINCREMENT, id_tarea INTEGER, fecha TEXT, duracion TEXT, FOREIGN KEY(id_tarea) REFERENCES tareas(id))";
            using (var cmd = new SQLiteCommand(sqlSesiones, conn))
            {
                cmd.ExecuteNonQuery();
            }
        }
    }

    static void AgregarTarea()
    {
        Console.Clear();
        programName();
        Console.Write("Ingrese el nombre de la tarea: ");
        string nombre = Console.ReadLine();

        using (var conn = new SQLiteConnection(connectionString))
        {
            conn.Open();
            string sql = "INSERT INTO tareas (nombre, tiempo) VALUES (@nombre, 0)";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@nombre", nombre);
                cmd.ExecuteNonQuery();
            }
        }

        Console.WriteLine("Tarea agregada correctamente.");
        Console.ReadKey();
    }

    static List<(int, string, int)> ObtenerTareas()
    {
        var tareas = new List<(int, string, int)>();
        using (var conn = new SQLiteConnection(connectionString))
        {
            conn.Open();
            string sql = "SELECT id, nombre, tiempo FROM tareas";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        tareas.Add((reader.GetInt32(0), reader.GetString(1), reader.GetInt32(2)));
                    }
                }
            }
        }
        return tareas;
    }

    static void MostrarTareas()
    {
        Console.Clear();
        programName();
        List<(int, string, int)> tareas = ObtenerTareas();
        if (tareas.Count == 0)
        {
            Console.WriteLine("No hay tareas disponibles.");
        }
        else
        {
            foreach (var tarea in tareas)
            {
                Console.WriteLine($"{tarea.Item1}. {tarea.Item2} - Tiempo total: {FormatoTiempo(tarea.Item3)}");
            }
        }
        Console.ReadKey();
    }

    static void SeleccionarTarea()
    {
        List<(int, string, int)> tareas = ObtenerTareas();
        if (tareas.Count == 0)
        {
            Console.Clear();
            programName();
            Console.WriteLine("No hay tareas disponibles.");
            Console.ReadKey();
            return;
        }

        Console.WriteLine("Seleccione una tarea:");
        for (int i = 0; i < tareas.Count; i++)
        {
            Console.WriteLine($"{i + 1}. {tareas[i].Item2} (Tiempo total: {FormatoTiempo(tareas[i].Item3)})");
        }

        if (int.TryParse(Console.ReadLine(), out int seleccion) && seleccion > 0 && seleccion <= tareas.Count)
        {
            int idTarea = tareas[seleccion - 1].Item1;
            string nombreTarea = tareas[seleccion - 1].Item2;
            int tiempoPrevio = tareas[seleccion - 1].Item3;

            Console.Clear();
            programName();
            Console.WriteLine($"Tarea: {nombreTarea}");
            Console.WriteLine("Sesiones previas:");
            List<string> sesiones = ObtenerSesiones(idTarea);
            if (sesiones.Count > 0)
            {
                foreach (var sesion in sesiones)
                {
                    Console.WriteLine(sesion);
                }
            }
            else
            {
                Console.WriteLine("No hay sesiones registradas.");
            }

            Console.WriteLine("\nPresione una tecla para iniciar la sesión...");
            Console.ReadKey();

            IniciarContador(idTarea, nombreTarea, tiempoPrevio);
        }
        else
        {
            Console.WriteLine("Selección inválida.");
            Console.ReadKey();
        }
    }

    static List<string> ObtenerSesiones(int idTarea)
    {
        var sesiones = new List<string>();
        using (var conn = new SQLiteConnection(connectionString))
        {
            conn.Open();
            string sql = "SELECT fecha, duracion FROM sesiones WHERE id_tarea = @id_tarea ORDER BY fecha DESC";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id_tarea", idTarea);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string fecha = reader.GetString(0);
                        string duracion = reader.GetString(1);
                        sesiones.Add($"[{fecha}] | Sesión: {duracion}");
                    }
                }
            }
        }
        return sesiones;
    }

    static void IniciarContador(int id, string nombre, int tiempoInicial)
    {
        Console.Clear();
        programName();
        Console.WriteLine($"Iniciando tarea: {nombre}");
        Stopwatch sw = new Stopwatch();
        sw.Start();

        // Iniciar la tarea que actualiza el tiempo en sesión
        var updateTask = Task.Run(async () => {
            while (sw.IsRunning)
            {
                Console.SetCursorPosition(0, 2); // Posicionar el cursor en la fila 2
                Console.Write(new string(' ', Console.WindowWidth)); // Limpiar la línea
                Console.SetCursorPosition(0, 2); // Volver a posicionar el cursor
                Console.WriteLine($"Tiempo en sesión: {FormatoTiempo((int)sw.Elapsed.TotalSeconds)}");
                await Task.Delay(1000); // Esperar 1 segundo
            }
        });

        Console.WriteLine();
        Console.WriteLine("Presione cualquier tecla para detener...");
        Console.ReadKey();
        sw.Stop();

        int tiempoTotal = tiempoInicial + (int)sw.Elapsed.TotalSeconds;
        string duracionSesion = FormatoTiempo((int)sw.Elapsed.TotalSeconds);
        string fechaSesion = DateTime.Now.ToString("dd/MM/yyyy HH:mm");

        using (var conn = new SQLiteConnection(connectionString))
        {
            conn.Open();

            string sqlUpdate = "UPDATE tareas SET tiempo = @tiempo WHERE id = @id";
            using (var cmd = new SQLiteCommand(sqlUpdate, conn))
            {
                cmd.Parameters.AddWithValue("@tiempo", tiempoTotal);
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
            }

            string sqlInsert = "INSERT INTO sesiones (id_tarea, fecha, duracion) VALUES (@id_tarea, @fecha, @duracion)";
            using (var cmd = new SQLiteCommand(sqlInsert, conn))
            {
                cmd.Parameters.AddWithValue("@id_tarea", id);
                cmd.Parameters.AddWithValue("@fecha", fechaSesion);
                cmd.Parameters.AddWithValue("@duracion", duracionSesion);
                cmd.ExecuteNonQuery();
            }
        }

        Console.WriteLine($"Tiempo total registrado: {FormatoTiempo(tiempoTotal)}. Presione una tecla para continuar...");
        Console.ReadKey();
    }

    static string FormatoTiempo(int segundos)
    {
        TimeSpan t = TimeSpan.FromSeconds(segundos);
        return $"{t.Hours:D2}:{t.Minutes:D2}:{t.Seconds:D2}";
    }

    static void programName()
    {
        string nombre = "RAMA | TASKTIMER";
        Console.WriteLine(nombre);
        Console.WriteLine();
    }

    static void EliminarTarea()
    {
        Console.Clear();
        programName();

        List<(int, string, int)> tareas = ObtenerTareas();
        if (tareas.Count == 0)
        {
            Console.WriteLine("No hay tareas para eliminar.");
            Console.ReadKey();
            return;
        }

        Console.WriteLine("Seleccione una tarea para eliminar:");
        for (int i = 0; i < tareas.Count; i++)
        {
            Console.WriteLine($"{i + 1}. {tareas[i].Item2} (Tiempo: {FormatoTiempo(tareas[i].Item3)})");
        }

        if (int.TryParse(Console.ReadLine(), out int seleccion) && seleccion > 0 && seleccion <= tareas.Count)
        {
            using (var conn = new SQLiteConnection(connectionString))
            {
                conn.Open();
                string sql = "DELETE FROM tareas WHERE id = @id";
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", tareas[seleccion - 1].Item1);
                    cmd.ExecuteNonQuery();
                }
            }
            Console.WriteLine("Tarea eliminada.");
        }
        else
        {
            Console.WriteLine("Selección inválida.");
        }
        Console.ReadKey();
    }
}
