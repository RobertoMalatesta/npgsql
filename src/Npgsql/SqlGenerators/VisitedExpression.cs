﻿#if ENTITIES
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.Metadata.Edm;
using System.Data.Common.CommandTrees;

namespace Npgsql.SqlGenerators
{
	internal abstract class VisitedExpression
	{
        protected VisitedExpression()
        {
            ExpressionList = new List<VisitedExpression>();
        }

        public void Append(VisitedExpression expression)
        {
            ExpressionList.Add(expression);
        }

        public void Append(string literal)
        {
            ExpressionList.Add(new LiteralExpression(literal));
        }

        public override string ToString()
        {
            StringBuilder sqlText = new StringBuilder();
            WriteSql(sqlText);
            return sqlText.ToString();
        }

        protected List<VisitedExpression> ExpressionList { get; private set; }

        internal virtual void WriteSql(StringBuilder sqlText)
        {
            foreach (VisitedExpression expression in ExpressionList)
            {
                expression.WriteSql(sqlText);
            }
        }
	}

    internal class LiteralExpression : VisitedExpression
    {
        private string _literal;

        public LiteralExpression(string literal)
        {
            _literal = literal;
        }

        public new void Append(VisitedExpression expresion)
        {
            base.Append(expresion);
        }

        public new void Append(string literal)
        {
            base.Append(literal);
        }

        internal override void WriteSql(StringBuilder sqlText)
        {
            sqlText.Append(_literal);
            base.WriteSql(sqlText);
        }
    }

    internal class ConstantExpression : VisitedExpression
    {
        private PrimitiveTypeKind _primitiveType;
        private object _value;

        public ConstantExpression(object value, TypeUsage edmType)
        {
            if (edmType == null)
                throw new ArgumentNullException("edmType");
            if (edmType.EdmType == null || edmType.EdmType.BuiltInTypeKind != BuiltInTypeKind.PrimitiveType)
                throw new ArgumentException("Require primitive EdmType", "edmType");
            _primitiveType = ((PrimitiveType)edmType.EdmType).PrimitiveTypeKind;
            _value = value;
        }

        internal override void WriteSql(StringBuilder sqlText)
        {
            switch (_primitiveType)
            {
                case PrimitiveTypeKind.Int16:
                case PrimitiveTypeKind.Int32:
                case PrimitiveTypeKind.Int64:
                case PrimitiveTypeKind.Decimal:
                    sqlText.Append(_value);
                    break;
                case PrimitiveTypeKind.String:
                    sqlText.Append("'" + _value + "'");
                    break;
                case PrimitiveTypeKind.Boolean:
                    sqlText.Append(_value.ToString().ToUpperInvariant());
                    break;
                case PrimitiveTypeKind.DateTime:
                    sqlText.AppendFormat("'{0:s}'", _value);
                    break;
            }
            base.WriteSql(sqlText);
        }
    }

    internal class UnionAllExpression : VisitedExpression
    {
        internal override void WriteSql(StringBuilder sqlText)
        {
            sqlText.Append(" UNION ALL ");
            base.WriteSql(sqlText);
        }
    }

    internal class ProjectionExpression : VisitedExpression
    {
        private bool requiresColumnSeperator;
        private InputExpression _from;

        public bool Distinct { get; set; }
        public InputExpression From
        {
            get { return _from; }
            set
            {
                _from = value;
                Append(" FROM ");
                Append(_from);
            }
        }

        internal override void WriteSql(StringBuilder sqlText)
        {
            sqlText.Append("SELECT ");
            if (Distinct)
                sqlText.Append("DISTINCT ");
            base.WriteSql(sqlText);
        }

        public void AppendColumn(VisitedExpression column)
        {
            if (requiresColumnSeperator)
                Append(",");
            Append(column);
            requiresColumnSeperator = true;
        }
    }

    internal class InsertExpression : VisitedExpression
    {
        public void AppendTarget(VisitedExpression target)
        {
            Append(target);
        }

        public void AppendColumns(IEnumerable<VisitedExpression> columns)
        {
            Append("(");
            bool first = true;
            foreach (VisitedExpression expression in columns)
            {
                if (!first)
                    Append(",");
                Append(expression);
                first = false;
            }
            Append(")");
        }

        public void AppendValues(IEnumerable<VisitedExpression> columns)
        {
            Append(" VALUES (");
            bool first = true;
            foreach (VisitedExpression expression in columns)
            {
                if (!first)
                    Append(",");
                Append(expression);
                first = false;
            }
            Append(")");
        }

        public VisitedExpression ReturningExpression { get; set; }

        internal override void WriteSql(StringBuilder sqlText)
        {
            sqlText.Append("INSERT INTO ");
            base.WriteSql(sqlText);
            if (ReturningExpression != null)
            {
                sqlText.Append(";");
                ReturningExpression.WriteSql(sqlText);
            }
        }
    }

    internal class UpdateExpression : VisitedExpression
    {
        private bool _setSeperatorRequired;

        public void AppendTarget(VisitedExpression target)
        {
            Append(target);
        }

        public void AppendSet(VisitedExpression property, VisitedExpression value)
        {
            if (_setSeperatorRequired)
                Append(",");
            else
                Append(" SET ");
            Append(property);
            Append("=");
            Append(value);
            _setSeperatorRequired = true;
        }

        public void AppendWhere(VisitedExpression where)
        {
            Append(" WHERE ");
            Append(where);
        }

        internal override void WriteSql(StringBuilder sqlText)
        {
            sqlText.Append("UPDATE ");
            base.WriteSql(sqlText);
        }
    }

    internal class DeleteExpression : VisitedExpression
    {
        public void AppendFrom(VisitedExpression from)
        {
            Append(from);
        }

        public void AppendWhere(VisitedExpression where)
        {
            Append(" WHERE ");
            Append(where);
        }

        internal override void WriteSql(StringBuilder sqlText)
        {
            sqlText.Append("DELETE FROM ");
            base.WriteSql(sqlText);
        }
    }

    internal class ColumnExpression : VisitedExpression
    {
        private VisitedExpression _column;
        private string _columnName;

        public ColumnExpression(VisitedExpression column, string columnName)
        {
            _column = column;
            _columnName = columnName;
        }

        internal override void WriteSql(StringBuilder sqlText)
        {
            _column.WriteSql(sqlText);
            sqlText.Append(" AS " + SqlBaseGenerator.QuoteIdentifier(_columnName));
            base.WriteSql(sqlText);
        }
    }

    internal class InputExpression : VisitedExpression
    {
        //public new void Append(VisitedExpression expresion)
        //{
        //    base.Append(expresion);
        //}

        //public new void Append(string literal)
        //{
        //    base.Append(literal);
        //}

        private WhereExpression _where;

        public WhereExpression Where
        {
            get { return _where; }
            set
            {
                _where = value;
                //Append(_where);
            }
        }

        private GroupByExpression _groupBy;

        public GroupByExpression GroupBy
        {
            get { return _groupBy; }
            set
            {
                _groupBy = value;
                //Append(_groupBy);
            }
        }

        private OrderByExpression _orderBy;

        public OrderByExpression OrderBy
        {
            get { return _orderBy; }
            set { _orderBy = value; }
        }

        private LimitExpression _limit;

        public LimitExpression Limit
        {
            get { return _limit; }
            set
            {
                _limit = value;
                //Append(" LIMIT ");
                //Append(_limit);
            }
        }

        internal override void WriteSql(StringBuilder sqlText)
        {
            base.WriteSql(sqlText);
            if (Where != null) Where.WriteSql(sqlText);
            if (GroupBy != null) GroupBy.WriteSql(sqlText);
            if (OrderBy != null) OrderBy.WriteSql(sqlText);
            if (Limit != null) Limit.WriteSql(sqlText);
        }
    }

    internal class FromExpression : InputExpression
    {
        private VisitedExpression _from;
        private string _name;
        static int _uniqueName = 1;

        public FromExpression(VisitedExpression from, string name)
        {
            _from = from;
            if (name != null)
            {
                _name = name;
            }
            else
            {
                _name = "ALIAS" + _uniqueName++;
            }
        }

        public string Name
        {
            get { return _name; }
        }

        internal override void WriteSql(StringBuilder sqlText)
        {
            bool wrap = !(_from is LiteralExpression);
            if (wrap)
                sqlText.Append("(");
            _from.WriteSql(sqlText);
            if (wrap)
                sqlText.Append(")");
            sqlText.Append(" AS ");
            sqlText.Append(SqlBaseGenerator.QuoteIdentifier(_name));
            base.WriteSql(sqlText);
        }
    }

    internal class JoinExpression : InputExpression
    {
        private VisitedExpression _left;
        private DbExpressionKind _joinType;
        private VisitedExpression _right;
        private VisitedExpression _condition;

        public JoinExpression(VisitedExpression left, DbExpressionKind joinType, VisitedExpression right, VisitedExpression condition)
        {
            _left = left;
            _joinType = joinType;
            _right = right;
            _condition = condition;
        }

        public VisitedExpression Condition
        {
            get { return _condition; }
            set { _condition = value; }
        }

        internal override void WriteSql(StringBuilder sqlText)
        {
            _left.WriteSql(sqlText);
            switch (_joinType)
            {
                case DbExpressionKind.InnerJoin:
                    sqlText.Append(" INNER JOIN ");
                    break;
                case DbExpressionKind.LeftOuterJoin:
                    sqlText.Append(" LEFT OUTER JOIN ");
                    break;
                case DbExpressionKind.FullOuterJoin:
                    sqlText.Append(" FULL OUTER JOIN ");
                    break;
                case DbExpressionKind.CrossJoin:
                    sqlText.Append(" CROSS JOIN ");
                    break;
                default:
                    throw new NotSupportedException();
            }
            _right.WriteSql(sqlText);
            if (_joinType != DbExpressionKind.CrossJoin)
            {
                sqlText.Append(" ON ");
                _condition.WriteSql(sqlText);
            }
            base.WriteSql(sqlText);
        }
    }

    internal class WhereExpression : VisitedExpression
    {
        private VisitedExpression _where;

        public WhereExpression(VisitedExpression where)
        {
            _where = where;
        }

        internal override void WriteSql(StringBuilder sqlText)
        {
            sqlText.Append(" WHERE ");
            _where.WriteSql(sqlText);
            base.WriteSql(sqlText);
        }

        internal void And(VisitedExpression andAlso)
        {
            _where = new BooleanExpression("AND", _where, andAlso);
        }
    }

    internal class VariableReferenceExpression : VisitedExpression
    {
        private string _name;
        private IDictionary<string, string> _variableSubstitution;

        public VariableReferenceExpression(string name, IDictionary<string, string> variableSubstitution)
        {
            _name = name;
            _variableSubstitution = variableSubstitution;
        }

        internal override void WriteSql(StringBuilder sqlText)
        {
            if (_variableSubstitution.ContainsKey(_name))
                sqlText.Append(SqlBaseGenerator.QuoteIdentifier(_variableSubstitution[_name]));
            else
            {
                // TODO: come up with a better solution
                // need some way of removing extra levels of dots
                if (_name.Contains("."))
                {
                    sqlText.Append(_name.Substring(_name.LastIndexOf('.') + 1));
                }
                else
                {
                    sqlText.Append(SqlBaseGenerator.QuoteIdentifier(_name));
                }
            }
            base.WriteSql(sqlText);
        }

        // override ToString since we don't want variable substitution
        // until writing out the SQL.
        public override string ToString()
        {
            StringBuilder unsubstitutedText = new StringBuilder();
            unsubstitutedText.Append(_name);
            foreach (var expression in this.ExpressionList)
            {
                unsubstitutedText.Append(expression.ToString());
            }
            return unsubstitutedText.ToString();
        }
    }

    internal class PropertyExpression : VisitedExpression
    {
        private VariableReferenceExpression _variable;
        private string _property;

        public PropertyExpression(VariableReferenceExpression variable, string property)
        {
            _variable = variable;
            _property = property;
        }

        internal override void WriteSql(StringBuilder sqlText)
        {
            _variable.WriteSql(sqlText);
            sqlText.Append(".");
            sqlText.Append(SqlBaseGenerator.QuoteIdentifier(_property));
            base.WriteSql(sqlText);
        }
    }

    internal class FunctionExpression : VisitedExpression
    {
        private string _name;
        private List<VisitedExpression> _args = new List<VisitedExpression>();

        public FunctionExpression(string name)
        {
            _name = name;
        }

        internal void AddArgument(VisitedExpression visitedExpression)
        {
            _args.Add(visitedExpression);
        }

        internal override void WriteSql(StringBuilder sqlText)
        {
            sqlText.Append(_name);
            sqlText.Append("(");
            bool first = true;
            foreach (var arg in _args)
            {
                if (!first)
                    sqlText.Append(",");
                arg.WriteSql(sqlText);
                first = false;
            }
            sqlText.Append(")");
            base.WriteSql(sqlText);
        }
    }

    internal class CastExpression : VisitedExpression
    {
        private VisitedExpression _value;
        private string _type;

        public CastExpression(VisitedExpression value, string type)
        {
            _value = value;
            _type = type;
        }

        internal override void WriteSql(StringBuilder sqlText)
        {
            sqlText.Append("CAST (");
            _value.WriteSql(sqlText);
            sqlText.AppendFormat(" AS {0})", _type);
            base.WriteSql(sqlText);
        }
    }

    internal class GroupByExpression : VisitedExpression
    {
        public void AppendGroupingKey(VisitedExpression key)
        {
            Append(key);
        }

        internal override void WriteSql(StringBuilder sqlText)
        {
            if (ExpressionList.Count != 0)
                sqlText.Append(" GROUP BY ");
            base.WriteSql(sqlText);
        }
    }

    internal class LimitExpression : VisitedExpression
    {
        private VisitedExpression _arg;

        public LimitExpression(VisitedExpression arg)
        {
            _arg = arg;
        }

        internal override void WriteSql(StringBuilder sqlText)
        {
            sqlText.Append(" LIMIT ");
            _arg.WriteSql(sqlText);
            base.WriteSql(sqlText);
        }
    }

    internal class BooleanExpression : VisitedExpression
    {
        private string _booleanOperator;
        private VisitedExpression _left;
        private VisitedExpression _right;

        public BooleanExpression(string booleanOperator, VisitedExpression left, VisitedExpression right)
        {
            _booleanOperator = booleanOperator;
            _left = left;
            _right = right;
        }

        internal override void WriteSql(StringBuilder sqlText)
        {
            bool wrapLeft = !(_left is PropertyExpression || _left is ConstantExpression);
            bool wrapRight = !(_right is PropertyExpression || _right is ConstantExpression);
            if (wrapLeft)
                sqlText.Append("(");
            _left.WriteSql(sqlText);
            if (wrapLeft)
                sqlText.Append(") ");
            sqlText.Append(_booleanOperator);
            if (wrapRight)
                sqlText.Append(" (");
            _right.WriteSql(sqlText);
            if (wrapRight)
                sqlText.Append(")");
            base.WriteSql(sqlText);
        }
    }

    internal class CombinedProjectionExpression : VisitedExpression
    {
        private VisitedExpression _first;
        private VisitedExpression _second;
        private string _setOperator;

        public CombinedProjectionExpression(VisitedExpression first, string setOperator, VisitedExpression second)
        {
            _first = first;
            _setOperator = setOperator;
            _second = second;
        }

        internal override void WriteSql(StringBuilder sqlText)
        {
            _first.WriteSql(sqlText);
            sqlText.Append(" ");
            sqlText.Append(_setOperator);
            sqlText.Append(" ");
            _second.WriteSql(sqlText);
            base.WriteSql(sqlText);
        }
    }

    internal class NegatableExpression : VisitedExpression
    {
        private bool _negated;

        protected bool Negated
        {
            get { return _negated; }
            set { _negated = value; }
        }

        public NegatableExpression Negate()
        {
            _negated = !_negated;
            // allows to be used inline
            return this;
        }
    }

    internal class ExistsExpression : NegatableExpression
    {
        private VisitedExpression _argument;

        public ExistsExpression(VisitedExpression argument)
        {
            _argument = argument;
        }

        internal override void WriteSql(StringBuilder sqlText)
        {
            if (Negated)
                sqlText.Append("NOT ");
            sqlText.Append("EXISTS (");
            _argument.WriteSql(sqlText);
            sqlText.Append(")");
            base.WriteSql(sqlText);
        }
    }

    internal class NegateExpression : NegatableExpression
    {
        private VisitedExpression _argument;

        public NegateExpression(VisitedExpression argument)
        {
            _argument = argument;
            Negated = true;
        }

        internal override void WriteSql(StringBuilder sqlText)
        {
            if (Negated)
                sqlText.Append(" NOT ");
            sqlText.Append("(");
            _argument.WriteSql(sqlText);
            sqlText.Append(")");
            base.WriteSql(sqlText);
        }
    }

    internal class IsNullExpression : NegatableExpression
    {
        private VisitedExpression _argument;

        public IsNullExpression(VisitedExpression argument)
        {
            _argument = argument;
        }

        internal override void WriteSql(StringBuilder sqlText)
        {
            _argument.WriteSql(sqlText);
            sqlText.Append(" IS ");
            if (Negated)
                sqlText.Append("NOT ");
            sqlText.Append("NULL ");
            base.WriteSql(sqlText);
        }
    }

    class OrderByExpression : VisitedExpression
    {
        private bool _requiresOrderSeperator;

        public void AppendSort(VisitedExpression sort, bool ascending)
        {
            if (_requiresOrderSeperator)
                Append(",");
            Append(sort);
            if (ascending)
                Append(" ASC ");
            else
                Append(" DESC ");
            _requiresOrderSeperator = true;
        }

        internal override void WriteSql(StringBuilder sqlText)
        {
            sqlText.Append(" ORDER BY ");
            base.WriteSql(sqlText);
        }
    }
}
#endif