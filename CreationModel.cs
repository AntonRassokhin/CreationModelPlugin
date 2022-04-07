using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CreationModelPlugin
{
    [TransactionAttribute(TransactionMode.Manual)]
    public class CreationModel : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;

            Level level1, level2;

            GetLevels(doc, out level1, out level2);
            CreateWalls(doc, level1, level2);

            /* ВОТ ЭТО ВСЕ НИЖЕ ПИСАЛИ КАК ПРИМЕР, ЧТОБЫ РАЗОБРАТЬ ФИЛЬТРЫ ДЛЯ СИСТЕМНЫХ И ЗАГРУЖАЕМЫХ СЕМЕЙСТВ
             
            var result1 = new FilteredElementCollector(doc) //ищем все стены в документе. Wall - это системное семейство, достаточно только фильтрации по классу
                .OfClass(typeof(Wall)) //ищем все объекты класса Wall (в объектно-ориентированном представлении).
                                       //Такую строку можно и убрать, т.к. ниже на 2 строки мы фильтруем через тип, но через тип - это linq фильтрация, а она ГОРАЗДО медленнее, чем фильтрация Revit
                                       //поэтому быстрее именно в таком виде
                //.Cast<Wall>() - это преобразование объектов в класс стена, так лучше не делать, т.к. при попадании в список чего-то кроме стены - будет исключение. Лучше см. следующую строку
                .OfType<Wall>() //выполняет фильтрацию на основе заданного типа, а не преобразование то есть выберет только стены
                .ToList();

            var result2 = new FilteredElementCollector(doc) //ищем все двери 
                .OfClass(typeof(FamilyInstance)) //ищем все объекты класса FamilyInstance
                .OfCategory(BuiltInCategory.OST_Doors) //добавляем фильтр по категории Doors, т.к. Doors - это загружаемое, а не системное семейство
                .OfType<FamilyInstance>() //отфиьтровали только FamilyInstance
                //.Where(x=>x.Name.Equals("0915 x 2134 мм") - это если мы хотим прямо конкретный типоразмер двери отфильтровать; вместо Equals можно было использовать ==
                .ToList();

            var result3 = new FilteredElementCollector(doc) // 
               .WhereElementIsNotElementType() 
               .ToList();
            */

            return Result.Succeeded;
        }

        private static void GetLevels (Document doc, out Level level1, out Level level2)
        {
            List<Level> listLevel = new FilteredElementCollector(doc) //отфильтруем все уровни документа в список
                .OfClass(typeof(Level))
                .OfType<Level>()
                .ToList();

            level1 = listLevel
                .Where(x=>x.Name.Equals("Уровень 1"))
                .FirstOrDefault(); //это чтобы взять нужный нам только 1 уровень, из коллекции
            level2 = listLevel
                .Where(x => x.Name.Equals("Уровень 2"))
                .FirstOrDefault(); //это чтобы взять нужный нам только 2 уровень, из коллекции
        }

        private static void CreateWalls(Document doc, Level level1, Level level2)
        {
            double width = UnitUtils.ConvertToInternalUnits(10000, UnitTypeId.Millimeters); //зададим ширину дома, приведя ее от 10000мм к системным еденицам
            double depth = UnitUtils.ConvertToInternalUnits(5000, UnitTypeId.Millimeters); //зададим глубину дома, приведя ее от 5000мм к системным еденицам
            double dx = width / 2;
            double dy = depth / 2; //получать координаты мы решили от центра нашего дома

            List<XYZ> points = new List<XYZ>(); //создаем список координат (помним, что координаты от центра дома)
            points.Add(new XYZ(-dx, -dy, 0));
            points.Add(new XYZ(dx, -dy, 0));
            points.Add(new XYZ(dx, dy, 0));
            points.Add(new XYZ(-dx, dy, 0));
            points.Add(new XYZ(-dx, -dy, 0)); //добавили пятую точку ровно аналогичную первой для простоты, т.к. строить стены мы будем в цикле, перебирая точки попарно: 1-2, 2-3, 3-4, 4-5

            List<Wall> walls = new List<Wall>(); //создали массив под стены на будущее

            Transaction tr = new Transaction(doc); //запускаем транзакцию для добавления в модель
            tr.Start("Построение стен");
            for (int i = 0; i < 4; i++) //перебираем циклом точки
            {
                Line line = Line.CreateBound(points[i], points[i + 1]); //на основании точек создаем линию
                Wall wall = Wall.Create(doc, line, level1.Id, false); // по линии создаем стену. !!! использовали .Id
                wall.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE).Set(level2.Id); //задаем высоту для каждой стены через отдельный параметр
                walls.Add(wall); //добавляем стену в массив на будущее                
            }
            tr.Commit();
        }

    }
}
