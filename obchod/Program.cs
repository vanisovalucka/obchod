using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Newtonsoft.Json;

namespace simulace
{
    public enum TypUdalosti
    {
        Start,
        Trpelivost,
        Obslouzen
    }

    public static class Generator
    {
        public static Random rnd = new(12345);
    }

    public class Udalost
    {
        public int kdy { get; set; }
        public Proces kdo { get; set; }
        public TypUdalosti co { get; set; }

        public Udalost() { }
        public Udalost(int kdy, Proces kdo, TypUdalosti co)
        {
            this.kdy = kdy;
            this.kdo = kdo;
            this.co = co;
        }
    }

    public class Kalendar
    {
        public List<Udalost> seznam { get; set; }
        public Kalendar()
        {
            seznam = new List<Udalost>();
        }
        public void Pridej(int kdy, Proces kdo, TypUdalosti co)
        {
            //foreach (Udalost ud in seznam)
            // if (ud.kdo == kdo)
            //Console.WriteLine("");

            seznam.Add(new Udalost(kdy, kdo, co));
        }
        public void Odeber(Proces kdo, TypUdalosti co)
        {
            foreach (Udalost ud in seznam)
            {
                if ((ud.kdo == kdo) && (ud.co == co))
                {
                    seznam.Remove(ud);
                    return;
                }
            }
        }
        public Udalost Prvni()
        {
            Udalost prvni = null;
            foreach (Udalost ud in seznam)
                if ((prvni == null) || (ud.kdy < prvni.kdy))
                    prvni = ud;
            seznam.Remove(prvni);
            return prvni;
        }
        public Udalost Vyber()
        {
            return Prvni();
        }
    }

    public abstract class Proces
    {
        public static char[] mezery = { ' ' };
        public int patro { get; set; }
        public string ID { get; set; }
        public abstract void Zpracuj(Udalost ud);
        public void log(string zprava) { }

        [JsonIgnore]
        public Model model { get; set; }
    }

    public class Oddeleni : Proces
    {
        public int rychlost { get; set; }
        public List<Zakaznik> fronta { get; set; }
        public bool obsluhuje { get; set; }

        public Oddeleni() { }
        public Oddeleni(Model model, string popis)
        {
            this.model = model;
            string[] popisy = popis.Split(Proces.mezery, StringSplitOptions.RemoveEmptyEntries);
            this.ID = popisy[0];
            this.patro = int.Parse(popisy[1]);
            if (this.patro > model.MaxPatro)
                model.MaxPatro = this.patro;
            this.rychlost = int.Parse(popisy[2]);
            obsluhuje = false;
            fronta = new List<Zakaznik>();
            model.VsechnaOddeleni.Add(this);
        }
        public void ZaradDoFronty(Zakaznik zak)
        {
            fronta.Add(zak);
            if (!obsluhuje)
            {
                obsluhuje = true;
                model.Naplanuj(model.Cas, this, TypUdalosti.Start);
            }
        }
        public void VyradZFronty(Zakaznik koho)
        {
            fronta.Remove(koho);
        }
        public override void Zpracuj(Udalost ud)
        {
            switch (ud.co)
            {
                case TypUdalosti.Start:
                    if (fronta.Count == 0)
                        obsluhuje = false;
                    else
                    {
                        Zakaznik zak = fronta[0];
                        fronta.RemoveAt(0);
                        model.Odplanuj(zak, TypUdalosti.Trpelivost);
                        model.Naplanuj(model.Cas + rychlost, zak, TypUdalosti.Obslouzen);
                        model.Naplanuj(model.Cas + rychlost, this, TypUdalosti.Start);
                    }
                    break;
            }
        }
    }

    public enum SmeryJizdy { Nahoru, Dolu, Stoji }

    public class Vytah : Proces
    {
        public int kapacita { get; set; }
        public int dobaNastupu { get; set; }
        public int dobaVystupu { get; set; }
        public int dobaPatro2Patro { get; set; }
        static int[] ismery = { +1, -1, 0 };

        public class Pasazer
        {
            public Proces kdo { get; set; }
            public int kamJede { get; set; }
            public Pasazer() { }
            public Pasazer(Proces kdo, int kamJede)
            {
                this.kdo = kdo;
                this.kamJede = kamJede;
            }
        }

        public List<Pasazer>[,] cekatele { get; set; }
        public List<Pasazer> naklad { get; set; }
        public SmeryJizdy smer { get; set; }
        public int kdyJsemMenilSmer { get; set; }

        public Vytah() { }
        public Vytah(Model model, string popis)
        {
            this.model = model;
            string[] popisy = popis.Split(Proces.mezery, StringSplitOptions.RemoveEmptyEntries);
            this.ID = popisy[0];
            this.kapacita = int.Parse(popisy[1]);
            this.dobaNastupu = int.Parse(popisy[2]);
            this.dobaVystupu = int.Parse(popisy[3]);
            this.dobaPatro2Patro = int.Parse(popisy[4]);
            this.patro = 0;
            this.smer = SmeryJizdy.Stoji;
            this.kdyJsemMenilSmer = -1;

            cekatele = new List<Pasazer>[model.MaxPatro + 1, 2];
            for (int i = 0; i < model.MaxPatro + 1; i++)
                for (int j = 0; j < 2; j++)
                    cekatele[i, j] = new List<Pasazer>();
            naklad = new List<Pasazer>();
        }

        public void PridejDoFronty(int odkud, int kam, Proces kdo)
        {
            Pasazer pas = new Pasazer(kdo, kam);
            if (kam > odkud)
                cekatele[odkud, (int)SmeryJizdy.Nahoru].Add(pas);
            else
                cekatele[odkud, (int)SmeryJizdy.Dolu].Add(pas);

            if (smer == SmeryJizdy.Stoji)
            {
                model.Odplanuj(model.vytah, TypUdalosti.Start);
                model.Naplanuj(model.Cas, this, TypUdalosti.Start);
            }
        }
        public bool CekaNekdoVPatrechVeSmeruJizdy()
        {
            int ismer = ismery[(int)smer];
            for (int pat = patro + ismer; (pat > 0) && (pat <= model.MaxPatro); pat += ismer)
                if ((cekatele[pat, (int)SmeryJizdy.Nahoru].Count > 0) || (cekatele[pat, (int)SmeryJizdy.Dolu].Count > 0))
                    return true;
            return false;
        }

        public override void Zpracuj(Udalost ud)
        {
            switch (ud.co)
            {
                case TypUdalosti.Start:
                    if (smer == SmeryJizdy.Stoji)
                    {
                        if (!naklad.Any() && !CekaNekdoVPatrechVeSmeruJizdy())
                        {
                            // Zkus otočit směr
                            smer = (smer == SmeryJizdy.Nahoru) ? SmeryJizdy.Dolu : SmeryJizdy.Nahoru;

                            // Pokud ani v novém směru nikdo není, jdi spát
                            if (!CekaNekdoVPatrechVeSmeruJizdy())
                            {
                                smer = SmeryJizdy.Stoji;
                                return; // Tady končíme, žádné Naplanuj!
                            }
                        }
                        // Pokud jsme našli práci v novém směru, pohneme se:
                        patro += ismery[(int)smer];
                        model.Naplanuj(model.Cas + dobaPatro2Patro, this, TypUdalosti.Start);
                    }
                    foreach (Pasazer pas in naklad)
                        if (pas.kamJede == patro)
                        {
                            naklad.Remove(pas);
                            pas.kdo.patro = patro;
                            model.Naplanuj(model.Cas + dobaVystupu, pas.kdo, TypUdalosti.Start);
                            model.Naplanuj(model.Cas + dobaVystupu, this, TypUdalosti.Start);
                            return;
                        }
                    if (naklad.Count == kapacita)
                    {
                        patro += ismery[(int)smer];
                        model.Naplanuj(model.Cas + dobaPatro2Patro, this, TypUdalosti.Start);
                        return;
                    }
                    else
                    {
                        if (cekatele[patro, (int)smer].Count > 0)
                        {
                            naklad.Add(cekatele[patro, (int)smer][0]);
                            cekatele[patro, (int)smer].RemoveAt(0);
                            model.Naplanuj(model.Cas + dobaNastupu, this, TypUdalosti.Start);
                            return;
                        }
                        if (naklad.Count > 0 || CekaNekdoVPatrechVeSmeruJizdy())
                        {
                            patro += ismery[(int)smer];
                            model.Naplanuj(model.Cas + dobaPatro2Patro, this, TypUdalosti.Start);
                            return;
                        }
                        if (smer == SmeryJizdy.Nahoru) smer = SmeryJizdy.Dolu;
                        else smer = SmeryJizdy.Nahoru;
                        if (kdyJsemMenilSmer != model.Cas)
                        {
                            kdyJsemMenilSmer = model.Cas;
                            model.Naplanuj(model.Cas + 1, this, TypUdalosti.Start);
                            return;
                        }
                        smer = SmeryJizdy.Stoji;
                        return;
                    }
            }
        }
    }

    public class Zakaznik : Proces
    {
        public int trpelivost { get; set; }
        public int prichod { get; set; }
        public bool obslouzen { get; set; }
        public bool podsimulace { get; set; } = false;
        public List<Oddeleni> Nakupy { get; set; }

        public Zakaznik() { }
        public Zakaznik(Model model)
        {
            this.model = model;
            this.prichod = Generator.rnd.Next(0, 601);
            this.trpelivost = Generator.rnd.Next(1, 181);
            this.obslouzen = false;
            Nakupy = new List<Oddeleni>();
            int pocet_nakupu = Generator.rnd.Next(1, 21);
            for (int i = 0; i < pocet_nakupu; i++)
            {
                int j = Generator.rnd.Next(1, model.VsechnaOddeleni.Count);
                Nakupy.Add(model.VsechnaOddeleni[j]);
            }
            this.patro = 0;
            model.Naplanuj(prichod, this, TypUdalosti.Start);
            model.VsichniZakaznici.Add(this);
        }
        public override void Zpracuj(Udalost ud)
        {
            switch (ud.co)
            {
                case TypUdalosti.Start:
                    if (Nakupy.Count == 0)
                    {
                        if (patro != 0) model.vytah.PridejDoFronty(patro, 0, this);
                        else { }
                    }
                    else
                    {
                        Oddeleni odd = Nakupy[0];
                        if (odd.patro == patro)
                        {
                            if (Nakupy.Count > 1) model.Naplanuj(model.Cas + trpelivost, this, TypUdalosti.Trpelivost);
                            odd.ZaradDoFronty(this);
                        }
                        else model.vytah.PridejDoFronty(patro, odd.patro, this);
                    }
                    break;
                case TypUdalosti.Obslouzen:
                    Nakupy.RemoveAt(0);
                    obslouzen = true;
                    this.model.Naplanuj(model.Cas, this, TypUdalosti.Start);
                    break;
                case TypUdalosti.Trpelivost:
                    Nakupy[0].VyradZFronty(this);
                    Oddeleni nesplneny = Nakupy[0];
                    Nakupy.RemoveAt(0);
                    Nakupy.Add(nesplneny);
                    model.Naplanuj(model.Cas, this, TypUdalosti.Start);
                    break;
            }
        }
    }

    public class SuperZakaznik_1 : Zakaznik
    {
        public SuperZakaznik_1() { }
        public SuperZakaznik_1(Model model) : base(model) { }

        public Oddeleni PrednostniNakup(List<Oddeleni> Nakupy)
        {
            foreach (Oddeleni odd in Nakupy)
                if (odd.patro == patro) return odd;
            return Nakupy[0];
        }

        public override void Zpracuj(Udalost ud)
        {
            switch (ud.co)
            {
                case TypUdalosti.Start:
                    if (Nakupy.Count == 0)
                    {
                        if (patro != 0) model.vytah.PridejDoFronty(patro, 0, this);
                        else { }
                    }
                    else
                    {
                        Oddeleni odd = PrednostniNakup(Nakupy);
                        if (odd.patro == patro)
                        {
                            if (Nakupy.Count > 1) model.Naplanuj(model.Cas + trpelivost, this, TypUdalosti.Trpelivost);
                            odd.ZaradDoFronty(this);
                        }
                        else model.vytah.PridejDoFronty(patro, odd.patro, this);
                    }
                    break;
                case TypUdalosti.Obslouzen:
                    // if (Nakupy.Count > 0)
                    Nakupy.RemoveAt(0);
                    obslouzen = true;
                    model.Naplanuj(model.Cas, this, TypUdalosti.Start);
                    break;
                case TypUdalosti.Trpelivost:
                    Nakupy[0].VyradZFronty(this);
                    Oddeleni nesplneny = Nakupy[0];
                    Nakupy.RemoveAt(0);
                    Nakupy.Add(nesplneny);
                    model.Naplanuj(model.Cas, this, TypUdalosti.Start);
                    break;
            }
        }
    }

    public class SuperZakaznik_2 : Zakaznik
    {
        public SuperZakaznik_2() { }
        public SuperZakaznik_2(Model model) : base(model) { }


        public int doba(Oddeleni odd)
        {

            Model model2 = this.model.Klonuj();
            int index_oddeleni = 0;
            for (int i = 0; i < this.model.VsechnaOddeleni.Count; i++)
            {
                if (this.model.VsechnaOddeleni[i] == odd) index_oddeleni = i;
            }
            odd = model2.VsechnaOddeleni[index_oddeleni];

            int index_zakaznika = 0;
            for (int i = 0; i < this.model.VsichniZakaznici.Count; i++)
            {
                if (this.model.VsichniZakaznici[i] == this) index_zakaznika = i;
            }
            Zakaznik JaVSimulaci = model2.VsichniZakaznici[index_zakaznika];


            JaVSimulaci.Nakupy = [odd];
            JaVSimulaci.obslouzen = false;


            if (odd.patro == JaVSimulaci.patro)
            {
                model2.Naplanuj(model2.Cas + trpelivost, JaVSimulaci, TypUdalosti.Trpelivost);

                odd.ZaradDoFronty(JaVSimulaci);
            }
            else model2.vytah.PridejDoFronty(JaVSimulaci.patro, odd.patro, JaVSimulaci);

            int doba = model2.ZaJakDlouhoZakaznikVyridiNakup(JaVSimulaci);



            return doba;
        }

        public Oddeleni NejvyhodnejsiOddeleni(List<Oddeleni> Nakupy)
        {
            Oddeleni nejvyhodnejsi = Nakupy[0];
            int NejkratsiDoba = doba(nejvyhodnejsi);
            int Doba;
            for (int i = 1; i < Nakupy.Count; i++)
            {
                Doba = doba(Nakupy[i]);
                if (Doba < NejkratsiDoba)
                {
                    NejkratsiDoba = Doba;
                    nejvyhodnejsi = Nakupy[i];
                }
            }
            return nejvyhodnejsi;
        }

        public override void Zpracuj(Udalost ud)
        {
            switch (ud.co)
            {
                case TypUdalosti.Start:
                    if (Nakupy.Count == 0)
                    {
                        if (patro != 0) model.vytah.PridejDoFronty(patro, 0, this);
                        else { }
                    }
                    else
                    {
                        Oddeleni odd;
                        if (this.podsimulace) odd = Nakupy[0];

                        else odd = NejvyhodnejsiOddeleni(Nakupy);

                        if (odd.patro == patro)
                        {
                            if (Nakupy.Count > 1) model.Naplanuj(model.Cas + trpelivost, this, TypUdalosti.Trpelivost);
                            odd.ZaradDoFronty(this);
                        }
                        else model.vytah.PridejDoFronty(patro, odd.patro, this);
                    }
                    break;
                case TypUdalosti.Obslouzen:
                    //if (Nakupy.Count > 0)
                    Nakupy.RemoveAt(0);
                    obslouzen = true;
                    model.Naplanuj(model.Cas, this, TypUdalosti.Start);
                    break;
                case TypUdalosti.Trpelivost:
                    Nakupy[0].VyradZFronty(this);
                    Oddeleni nesplneny = Nakupy[0];
                    Nakupy.RemoveAt(0);
                    Nakupy.Add(nesplneny);
                    model.Naplanuj(model.Cas, this, TypUdalosti.Start);
                    break;
            }
        }
    }

    public class Model
    {
        public int Cas { get; set; }
        public Vytah vytah { get; set; }
        [JsonIgnore]
        public Dictionary<Zakaznik, int> zakaznici { get; set; } = new Dictionary<Zakaznik, int>();

        public List<Zakaznik> VsichniZakaznici { get; set; } = new List<Zakaznik>();
        public List<Oddeleni> VsechnaOddeleni { get; set; } = new List<Oddeleni>();
        public int MaxPatro { get; set; }
        public Kalendar kalendar { get; set; }

        public void Naplanuj(int kdy, Proces kdo, TypUdalosti co) => kalendar.Pridej(kdy, kdo, co);
        public void Odplanuj(Proces kdo, TypUdalosti co) => kalendar.Odeber(kdo, co);

        public void VytvorProcesy(int pocet_zakazniku)
        {
            System.IO.StreamReader soubor = new System.IO.StreamReader("obchod_data.txt");
            while (!soubor.EndOfStream)
            {
                string s = soubor.ReadLine();
                if (s != "")
                {
                    switch (s[0])
                    {
                        case 'O': new Oddeleni(this, s.Substring(1)); break;
                        case 'V': vytah = new Vytah(this, s.Substring(1)); break;
                    }
                }
            }

            for (int i = 0; i < pocet_zakazniku; i++)
            {
                Zakaznik zakaznik = null;
                switch (i % 3)
                {
                    case 1:
                        zakaznik = new Zakaznik(this); break;
                    case 2:
                        zakaznik = new SuperZakaznik_1(this); break;
                    case 0:
                        zakaznik = new SuperZakaznik_2(this); break;
                }
                zakaznici.Add(zakaznik, 0);

            }

            soubor.Close();
        }

        public int Vypocet(int pocet_zakazniku)
        {
            Cas = 0;
            kalendar = new Kalendar();
            VytvorProcesy(pocet_zakazniku);
            Udalost ud;
            while ((ud = kalendar.Vyber()) != null)
            {
                Cas = ud.kdy;
                ud.kdo.Zpracuj(ud);
                if (ud.kdo is Zakaznik z) zakaznici[z] = Cas - z.prichod;
            }
            int sum0 = 0;
            int sum1 = 0;
            int sum2 = 0;
            int amount0 = 0;
            int amount1 = 0;
            int amount2 = 0;
            foreach (Zakaznik z in zakaznici.Keys)
            {
                if (z is SuperZakaznik_1) { sum1 += zakaznici[z]; amount1++; }
                else if (z is SuperZakaznik_2) { sum2 += zakaznici[z]; amount2++; }
                else { sum0 += zakaznici[z]; amount0++; }
            }
            Console.WriteLine("\n" + pocet_zakazniku + "  " + (amount0 > 0 ? sum0 / amount0 : 0) + "  " + (amount1 > 0 ? sum1 / amount1 : 0) + "  " + (amount2 > 0 ? sum2 / amount2 : 0));
            return Cas;
        }

        public int ZaJakDlouhoZakaznikVyridiNakup(Zakaznik z)
        {
            int zacatek = Cas;
            Udalost ud;
            while ((ud = kalendar.Vyber()) != null)
            {
               
                if (ud.kdo == z && z.obslouzen)
                    return Cas - zacatek;
                Cas = ud.kdy;
                ud.kdo.Zpracuj(ud);
            }
            return Cas;
        }

        public Model Klonuj()
        {
            // 1. Nastavení pro Newtonsoft.Json
            var settings = new JsonSerializerSettings
            {
                // EXTRÉMNĚ DŮLEŽITÉ: Uloží do JSONu informaci o tom, že jde o SuperZakaznika.
                // Bez toho by se ti při načtení (deserializaci) vytvořil jen obyčejný Zakaznik.
                TypeNameHandling = TypeNameHandling.Auto,

                // Zachová propojení objektů (pokud by na jeden objekt mířilo víc odkazů)
                PreserveReferencesHandling = PreserveReferencesHandling.Objects
            };

            // 2. Serializace (převod na text)
            string json = JsonConvert.SerializeObject(this, settings);

            // 3. Deserializace (vytvoření nového světa z textu)
            Model klon = JsonConvert.DeserializeObject<Model>(json, settings);


            // 4. Ruční oprava propojení (Relink)
            if (klon != null)
            {
                // A) Propoj základní infrastrukturu
                if (klon.vytah != null) klon.vytah.model = klon;

                foreach (var odd in klon.VsechnaOddeleni)
                {
                    odd.model = klon;
                    // RELINK: Zákazníci, kteří už stojí ve frontě u pultu
                    foreach (var zakVeFronte in odd.fronta)
                    {
                        zakVeFronte.model = klon;
                        zakVeFronte.podsimulace = true;
                    }
                }

                // B) Propoj seznam všech existujících zákazníků
                foreach (var zak in klon.VsichniZakaznici)
                {
                    zak.model = klon;
                    zak.podsimulace = true;
                }

                // C) Propoj vše uvnitř výtahu (náklad a čekající v patrech)
                if (klon.vytah != null)
                {
                    // Pasažéři uvnitř kabiny
                    foreach (var pasazer in klon.vytah.naklad)
                    {
                        if (pasazer.kdo != null) pasazer.kdo.model = klon;
                    }

                    // Čekající v patrech (2D pole seznamů)
                    for (int p = 0; p <= klon.MaxPatro; p++)
                    {
                        for (int s = 0; s < 2; s++)
                        {
                            if (klon.vytah.cekatele[p, s] != null)
                            {
                                foreach (var pasazer in klon.vytah.cekatele[p, s])
                                {
                                    if (pasazer.kdo != null)
                                    {
                                        pasazer.kdo.model = klon;
                                        if (pasazer.kdo is Zakaznik z) z.podsimulace = true;
                                    }
                                }
                            }
                        }
                    }
                }

                // D) Propoj události, které už jsou v kalendáři
                if (klon.kalendar?.seznam != null)
                {
                    foreach (var ud in klon.kalendar.seznam)
                    {
                        if (ud.kdo != null)
                        {
                            ud.kdo.model = klon;
                            if (ud.kdo is Zakaznik z) z.podsimulace = true;
                        }
                    }
                }
            }

            return klon;
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            Model model1 = new Model();
            for (int pocet = 1; pocet < 302; pocet += 10)
                model1.Vypocet(pocet);
            Console.ReadLine();
        }
    }
}